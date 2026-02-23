using LibVLCSharp.Shared;
using CleanDiscPlayer.Core.Playback;
using Microsoft.Extensions.Logging;

namespace CleanDiscPlayer.WindowsPlayer.Playback
{
    public class WindowsPlaybackService : IPlaybackService
    {
        private readonly ILogger<WindowsPlaybackService> _logger;
        private bool _initialized = false;
        private LibVLC _libVlc;
        private MediaPlayer _mediaPlayer;
        private string _currentDrive;
        private int _currentTrack = 0;
        private int _trackCount = 0;

        // Playback state
        private RepeatMode _repeatMode = RepeatMode.NoRepeat;
        private bool _shuffleEnabled = false;

        // Queue: remaining tracks to play this cycle
        private Queue<int> _playQueue = new Queue<int>();

        // History: tracks we've already played (for previous button)
        private Stack<int> _playHistory = new Stack<int>();

        /// <summary>
        /// Repeat mode options for playback.
        /// </summary>
        private enum RepeatMode
        {
            NoRepeat,       // Stop after last track
            RepeatAll,      // Loop back to first track
            RepeatTrack     // Repeat current track forever
        }

        private const int MinVolume = 0;
        private const int MaxVolume = 100;

        /// <summary>
        /// Initializes a new instance of WindowsPlaybackService.
        /// </summary>
        /// <param name="logger">Logger for diagnostic and playback information.</param>
        public WindowsPlaybackService(ILogger<WindowsPlaybackService> logger)
        {
            _logger = logger;
            _logger.LogDebug("WindowsPlaybackService created");
        }

        public void Init(string driveLetter, int trackCount)
        {
            if (_initialized)
            {
                _logger.LogError("Attempted to initialize already-initialized playback service");
                throw new InvalidOperationException("Already initialized");
            }

            _logger.LogInformation("Initializing playback service for drive: {Drive}, {TrackCount} tracks", driveLetter, trackCount);

            _initialized = true;
            _currentDrive = driveLetter.TrimEnd('\\').TrimEnd('/');
            _trackCount = trackCount;

            ResetPlayQueueAndHistory();
            // TODO: initialize repeat mode and shuffle mode based on user settings???

            try
            {
                // TODO: why does this only sometimes fail???
                LibVLCSharp.Shared.Core.Initialize();
                _logger.LogDebug("LibVLC Core initialized");

                // Verbose logging for debugging
                //_libVlc = new LibVLC("--verbose=2");
                //_libVlc.Log += (sender, e) => _logger.LogDebug("[VLC] {Level}: {Message}", e.Level, e.Message);

                _libVlc = new LibVLC();
                _mediaPlayer = new MediaPlayer(_libVlc);

                // EndReached fires on a VLC internal thread, so we must dispatch
                // the next track call onto a separate thread to avoid deadlocking VLC
                _mediaPlayer.EndReached += (sender, e) =>
                {
                    Task.Run(() => AdvanceTrack());
                };

                _logger.LogInformation("Playback service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize LibVLC");
                throw;
            }
        }

        /// <summary>
        /// Clears the play queue and play history, resetting both to an empty state.
        /// </summary>
        /// <remarks>Call this method to remove all items from the play queue and history, typically when
        /// starting a new playback session or resetting the player state.</remarks>
        private void ResetPlayQueueAndHistory()
        {
            _playQueue.Clear();
            _playHistory.Clear();
            _logger.LogDebug("Play queue and history reset");
        }

        /// <summary>
        /// Called automatically when the current track ends.
        /// </summary>
        private void AdvanceTrack()
        {
            _logger.LogDebug("Track ended, advancing (repeat mode: {RepeatMode})", _repeatMode);
            if (_repeatMode == RepeatMode.RepeatTrack)
            {
                // replay current track and don't update play history or queue 
                _logger.LogInformation("Repeating track {TrackNumber}", _currentTrack);
                PlayTrack(_currentTrack);

                LogQueueState();
            }
            else
            {
                PlayNextTrack();
            }
            // Console.Write("Enter command: ");
        }

        /// <summary>
        /// Advances playback to the next track in the queue, handling repeat and playback completion according to the
        /// current repeat mode.
        /// </summary>
        /// <remarks>If the play queue is empty and the repeat mode is set to repeat all, the queue is
        /// refilled from the play history and playback continues from the beginning. Otherwise, playback is stopped
        /// when the end of the queue is reached.</remarks>
        private void PlayNextTrack()
        {
            if (_playQueue.Count > 0)
            {
                int nextTrack = _playQueue.Dequeue();
                _playHistory.Push(_currentTrack);

                _logger.LogInformation("Advancing to next track: {NextTrack}", nextTrack);
                PlayTrack(nextTrack);

                LogQueueState();
            }
            else if (_playQueue.Count == 0)
            {
                if (_repeatMode == RepeatMode.RepeatAll)
                {
                    _logger.LogInformation("Queue empty, refilling from history (Repeat All mode)");

                    // reverse history to get correct order in queue and include current track
                    _playQueue = new Queue<int>(_playHistory.Reverse());
                    _playQueue.Enqueue(_currentTrack);
                    _playHistory.Clear();
                    PlayTrack(_playQueue.Dequeue());

                    LogQueueState("Queue refilled from history");
                }
                else
                {
                    _logger.LogInformation("End of queue reached, stopping playback");
                    //Console.WriteLine("\nEnd of Queue, Stopping Playback.");
                    StopPlayback();
                }
            }
        }

        public void PlayFromBeginning()
        {
            _logger.LogInformation("Starting playback from beginning");
            PlayFromTrack(1);
        }


        public void PlayFromBeginningOfQueue()
        {
            if (_playQueue.Count == 0)
            {
                _logger.LogWarning("Cannot play from beginning of queue - queue is empty");
                return;
            }

            int nextTrack = _playQueue.Dequeue();

            _logger.LogInformation("Playing from beginning of queue: track {TrackNumber}", nextTrack);
            PlayTrack(nextTrack);

            LogQueueState();
        }

        public void PlayFromTrack(int trackNumber)
        {
            if (!IsValidTrack(trackNumber, out string error))
            {
                _logger.LogWarning("Invalid track number {TrackNumber}: {Error}", trackNumber, error);
                return;
            }
            _logger.LogInformation("Playing from track {TrackNumber} (shuffle: {ShuffleEnabled})", trackNumber, _shuffleEnabled);

            // if user selects a track number from queue, we need to jump to it and update the queue and history
            // if user selects a track number not in queue, we need to find it in history and update the queue and history
            // if shuffle mode is enabled, create new shuffle queue from that track
            ResetPlayQueueAndHistory();
            PlayTrack(trackNumber);
            //BuildQueueFromTrack(trackNumber);

            if (_shuffleEnabled)
            {
                ShuffleQueue();
            }
            else
            {
                BuildQueueFromTrack(trackNumber);
            }
        }

        /// <summary>
        /// Builds a sequential play queue starting from the specified track.
        /// </summary>
        /// <param name="trackNumber">Track number to start queue from.</param>
        private void BuildQueueFromTrack(int trackNumber)
        {
            _playQueue = new Queue<int>(Enumerable.Range(trackNumber + 1, _trackCount - trackNumber));
            _playHistory = new Stack<int>(Enumerable.Range(1, trackNumber - 1));

            _logger.LogDebug("Built sequential queue from track {TrackNumber}", trackNumber);
            LogQueueState();
        }

        /// <summary>
        /// Plays the specified track in the media player.
        /// </summary>
        /// <param name="trackNumber">Track number to play (1-based).</param>
        private void PlayTrack(int trackNumber)
        {
            if (!IsValidTrack(trackNumber, out string error))
            {
                _logger.LogWarning("Invalid track number {TrackNumber}: {Error}", trackNumber, error);
                return;
            }

            _logger.LogDebug("Playing track {TrackNumber} from drive {Drive}", trackNumber, _currentDrive);

            var mediaPath = $"cdda:///{_currentDrive}";
            var media = new Media(_libVlc, mediaPath, FromType.FromLocation);
            media.AddOption($":cdda-track={trackNumber}");

            _currentTrack = trackNumber;
            _mediaPlayer.Play(media);

            //Console.WriteLine($"\nPlaying track {_currentTrack} of {_trackCount}");
            _logger.LogInformation("Now playing track {TrackNumber} of {TrackCount}", _currentTrack, _trackCount);
        }

        /// <summary>
        /// Determines whether the specified track number is valid for the current disc and provides an error message if
        /// it is not.
        /// </summary>
        /// <param name="trackNumber">The track number to validate. Must be greater than or equal to 1 and less than or equal to the total number
        /// of tracks on the disc.</param>
        /// <param name="errorMessage">When the method returns <see langword="false"/>, contains a message describing why the track number is
        /// invalid; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the track number is valid; otherwise, <see langword="false"/>.</returns>
        private bool IsValidTrack(int trackNumber, out string errorMessage)
        {
            if (trackNumber < 1)
            {
                errorMessage = "Track number must be positive and start from 1.";
                return false;
            }
            if (trackNumber > _trackCount)
            {
                errorMessage = $"Track {trackNumber} exceeds disc track count ({_trackCount}).";
                return false;
            }
            errorMessage = null;
            return true;
        }


        public void PausePlayback()
        {
            _mediaPlayer?.Pause();
            _logger.LogInformation("Playback paused");
        }

        public void ResumePlayback()
        {
            _mediaPlayer?.Play();
            _logger.LogInformation("Playback resumed");
        }

        public void SkipToNextTrack()
        {
            _logger.LogInformation("User requested skip to next track");
            PlayNextTrack();
        }

        public void SkipToPreviousTrack()
        {
            _logger.LogInformation("User requested skip to previous track");

            // If more than 5 seconds have elapsed in the current track, restart it.
            // Otherwise, go back to previous track in history if there is one. If there is no previous track in history, just restart current track.
            if ((_mediaPlayer?.State != VLCState.NothingSpecial && _mediaPlayer?.State != VLCState.Ended && _mediaPlayer?.State != VLCState.Stopped) && (_mediaPlayer?.Time) < 5000)
            {
                if (_playHistory.Count == 0)
                {
                    _logger.LogDebug("No previous track in history, restarting current track");
                    PlayTrack(_currentTrack);
                    return;
                }

                _playQueue = new Queue<int>(new[] { _currentTrack }.Concat(_playQueue));
                int previousTrack = _playHistory.Pop();

                _logger.LogInformation("Going back to previous track: {PreviousTrack}", previousTrack);
                PlayTrack(previousTrack);
            }
            else
            {
                _logger.LogDebug("More than 5 seconds elapsed, restarting current track");
                // replay current track and don't update play history or queue 
                PlayTrack(_currentTrack);
            }

            LogQueueState();
        }

        public void StopPlayback()
        {
            ResetPlayQueueAndHistory();
            _currentTrack = 0;
            _mediaPlayer?.Stop();
            _logger.LogInformation("Playback stopped");
        }

        public void ChangeVolume(int volume)
        {
            if (volume < MinVolume || volume > MaxVolume)
            {
                _logger.LogWarning("Invalid volume {Volume} - must be between {Min} and {Max}", volume, MinVolume, MaxVolume);
                return;
            }
            _mediaPlayer.Volume = volume;
            _logger.LogInformation("Volume set to {Volume}%", volume);
        }


        
        public void SeekTo(string seekTimeStr)
        {
            _logger.LogDebug("Seek requested to: {SeekTime}", seekTimeStr);

            // https://learn.microsoft.com/en-us/dotnet/api/system.timespan.tryparse?view=net-10.0
            // If format is "mm:ss", prepend "00:" to make it "00:mm:ss"
            string normalized = seekTimeStr.Split(':').Length == 2
                ? $"00:{seekTimeStr}"
                : seekTimeStr;

            if (!TimeSpan.TryParse(normalized, out TimeSpan seekTime))
            {
                _logger.LogWarning("Invalid time format: {SeekTimeStr}", seekTimeStr);
                return;
            }

            long trackLengthMs = _mediaPlayer.Length;
            long seekMs = (long)seekTime.TotalMilliseconds;

            if (seekMs > trackLengthMs)
            {
                _logger.LogWarning("Cannot seek to {SeekTime} - track is only {TrackLength} long", seekTime.ToString(@"mm\:ss"), TimeSpan.FromMilliseconds(trackLengthMs).ToString(@"mm\:ss"));
                return;
            }

            _mediaPlayer.Time = seekMs;
            _logger.LogInformation("Seeked to {SeekTime}", seekTime.ToString(@"mm\:ss"));
        }

        public int CurrentTrack()
        {
            return _currentTrack;
        }

        public int TrackCount()
        {
            return _trackCount;
        }

        public Queue<int> GetPlayQueue()
        {
            return new Queue<int>(_playQueue);
        }

        public void ShuffleQueue()
        {
            _logger.LogInformation("Shuffling play queue");
            ResetPlayQueueAndHistory();
            
            if (_currentTrack == 0)
            {
                // if no track is currently playing, shuffle entire tracklist
                var allTracks = Enumerable.Range(1, _trackCount).ToList();
                var shuffled = allTracks.OrderBy(x => Guid.NewGuid()).ToList();
                _playQueue = new Queue<int>(shuffled);

                _logger.LogDebug("Shuffled entire tracklist ({TrackCount} tracks)", _trackCount);
            }
            else
            {
                // if a track is currently playing, shuffle all the other tracks and put them in the queue
                var remainingTracks = Enumerable.Range(1, _trackCount)
                    .Where(t => t != _currentTrack)
                    .ToList();
                var shuffled = remainingTracks.OrderBy(x => Guid.NewGuid()).ToList();
                _playQueue = new Queue<int>(shuffled);

                _logger.LogDebug("Shuffled {TrackCount} remaining tracks (current track: {CurrentTrack})", remainingTracks.Count, _currentTrack);
            }

            LogQueueState("Shuffle enabled");
        }

        public void LogCurrentState()
        {
            _logger.LogInformation("=== Player State ===");
            _logger.LogInformation("Current Track: {CurrentTrack} of {TrackCount}", _currentTrack, _trackCount);
            _logger.LogInformation("Media Player State: {State}", _mediaPlayer?.State);
            _logger.LogInformation("Volume: {Volume}%", _mediaPlayer?.Volume);
            _logger.LogInformation("Repeat Mode: {RepeatMode}", _repeatMode);
            _logger.LogInformation("Shuffle Enabled: {ShuffleEnabled}", _shuffleEnabled);

            //Console.WriteLine($"\nCurrent Track: {_currentTrack}");
            //Console.WriteLine($"Track Count: {_trackCount}");
            //Console.WriteLine($"Media Player State: {_mediaPlayer?.State}");
            //Console.WriteLine($"Volume: {_mediaPlayer?.Volume}%");

            // https://stackoverflow.com/questions/68367609/vlc-libvlc-state-t-state-machine
            // if media player state is nothing special or ended, then dont compute these lines:
            if (_mediaPlayer?.State != VLCState.NothingSpecial && _mediaPlayer?.State != VLCState.Ended && _mediaPlayer?.State != VLCState.Stopped)
            {
                _logger.LogInformation("Seekable: {Seekable}", _mediaPlayer?.IsSeekable);
                _logger.LogInformation("Time: {Time}ms", _mediaPlayer?.Time);
                _logger.LogInformation("Position: {Position}%", _mediaPlayer?.Position);
                _logger.LogInformation("Length: {Length}ms", _mediaPlayer?.Length);

                //Console.WriteLine($"Seekable: {_mediaPlayer?.IsSeekable}");
                //Console.WriteLine($"Time (ms): {_mediaPlayer?.Time}");
                //Console.WriteLine($"Position (%): {_mediaPlayer?.Position}");
                //Console.WriteLine($"Length (ms): {_mediaPlayer?.Length}\n");
            }

            LogQueueState("Current state");
            _logger.LogInformation("===================");
        }

        public (TimeSpan Time, TimeSpan Length, float Position, int Volume, bool ShuffleMode, string CurrentRepeatMode) GetCurrentTrackProgress() {
            // https://stackoverflow.com/questions/68367609/vlc-libvlc-state-t-state-machine
            // if media player state is nothing special or ended, then dont compute these lines:
            if (_mediaPlayer?.State != VLCState.NothingSpecial && _mediaPlayer?.State != VLCState.Ended && _mediaPlayer?.State != VLCState.Stopped && _currentTrack > 0 && _currentTrack <= _trackCount)
            {
                string repeatMode = "OFF";

                if (_repeatMode == RepeatMode.RepeatAll)
                    repeatMode = "ALL";
                else if (_repeatMode == RepeatMode.RepeatTrack)
                    repeatMode = "TRACK";


                    return (
                        TimeSpan.FromMilliseconds((double)_mediaPlayer.Time),
                        TimeSpan.FromMilliseconds((double)_mediaPlayer.Length),
                        _mediaPlayer.Position,
                        _mediaPlayer.Volume,
                        _shuffleEnabled,
                        repeatMode
                    );
            }
            else
            {
                return (TimeSpan.Zero, TimeSpan.Zero, 0f, 0, false, "OFF");
            }
        }

        public void SetRepeatMode(int mode)
        {
            // implement repeat mode (3 modes: no repeat, repeat disc, repeat track)
            // TODO: clean this up by using an enum for repeat mode instead of int and removing the if statements
            if (mode < 0 || mode > 2)
            {
                //Console.WriteLine("Invalid repeat mode. Valid modes are: 0 (no repeat), 1 (repeat disc), 2 (repeat track).");
                _logger.LogWarning("Invalid repeat mode {Mode} - valid modes are 0-2", mode);
                return;
            }
            else if (mode == 0)
            {
                _repeatMode = RepeatMode.NoRepeat;
                //Console.WriteLine("Repeat Mode set to No Repeat.");
                _logger.LogInformation("Repeat mode set to: No Repeat");
            }
            else if (mode == 1)
            {
                _repeatMode = RepeatMode.RepeatAll;
                //Console.WriteLine("Repeat Mode set to Repeat All.");
                _logger.LogInformation("Repeat mode set to: Repeat All");

            }
            else if (mode == 2)
            {
                _repeatMode = RepeatMode.RepeatTrack;
                //Console.WriteLine("Repeat Mode set to Repeat Track.");
                _logger.LogInformation("Repeat mode set to: Repeat Track");
            }
        }

        public void SetShuffleMode(bool enabled)
        {
            _shuffleEnabled = enabled;
            _logger.LogInformation("Shuffle mode {Status}", enabled ? "enabled" : "disabled");

            if (enabled)
            {
                ShuffleQueue();
            }
            else
            {
                ClearShuffleQueue();
            }
        }

        /// <summary>
        /// Removes all items from the shuffle queue and rebuilds a normal queue.
        /// </summary>
        private void ClearShuffleQueue()
        {
            _logger.LogDebug("Clearing shuffle queue and rebuilding sequential queue");
            ResetPlayQueueAndHistory();
            BuildQueueFromTrack(_currentTrack);
        }

        /// <summary>
        /// Logs the current state of the play queue and history for debugging.
        /// </summary>
        /// <param name="context">Optional context message to include in the log.</param>
        private void LogQueueState(string? context = null)
        {
            //Console.WriteLine($"Current Track: {_currentTrack}");
            //Console.WriteLine($"Queue: {string.Join(", ", _playQueue)}");
            //Console.WriteLine($"History: {string.Join(", ", _playHistory)}");

            var contextMsg = string.IsNullOrEmpty(context) ? "" : $" ({context})";
            _logger.LogDebug("Queue state{Context}: Current={Current}, Queue=[{Queue}], History=[{History}]",
                contextMsg,
                _currentTrack,
                string.Join(", ", _playQueue),
                string.Join(", ", _playHistory));
        }
    }
}
