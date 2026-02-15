using LibVLCSharp.Shared;
using CleanDiscPlayer.Core.Playback;

namespace CleanDiscPlayer.WindowsPlayer.Playback
{
    public class WindowsPlaybackService : IPlaybackService
    {
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

        private enum RepeatMode
        {
            NoRepeat,      // Stop after last track
            RepeatAll,    // Loop back to first track
            RepeatTrack    // Repeat current track forever
        }

        private const int MinVolume = 0;
        private const int MaxVolume = 100;

        public void Init(string driveLetter, int trackCount)
        {
            if (_initialized)
                throw new InvalidOperationException("Already initialized");

            _initialized = true;
            _currentDrive = driveLetter.TrimEnd('\\').TrimEnd('/');
            _trackCount = trackCount;

            ResetPlayQueueAndHistory();
            // TODO: initialize repeat mode and shuffle mode based on user settings???

            LibVLCSharp.Shared.Core.Initialize();

            // Verbose logging for debugging
            //_libVlc = new LibVLC("--verbose=2");
            //_libVlc.Log += (sender, e) => Console.WriteLine($"[VLC] {e.Level}: {e.Message}");

            _libVlc = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVlc);

            // EndReached fires on a VLC internal thread, so we must dispatch
            // the next track call onto a separate thread to avoid deadlocking VLC
            _mediaPlayer.EndReached += (sender, e) =>
            {
                Task.Run(() => AdvanceTrack());
            };
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
        }

        /// <summary>
        /// Called automatically when the current track ends.
        /// </summary>
        private void AdvanceTrack()
        {
            if (_repeatMode == RepeatMode.RepeatTrack)
            {
                // replay current track and don't update play history or queue 
                PlayTrack(_currentTrack);

                Console.WriteLine($"Current Track: {_currentTrack}");
                Console.WriteLine($"Queue: {string.Join(", ", _playQueue)}");
                Console.WriteLine($"History: {string.Join(", ", _playHistory)}");
            }
            else
            {
                PlayNextTrack();
            }
            Console.Write("Enter command: ");
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
                PlayTrack(nextTrack);

                Console.WriteLine($"Current Track: {_currentTrack}");
                Console.WriteLine($"Queue: {string.Join(", ", _playQueue)}");
                Console.WriteLine($"History: {string.Join(", ", _playHistory)}");
            }
            else if (_playQueue.Count == 0)
            {
                if (_repeatMode == RepeatMode.RepeatAll)
                {
                    // reverse history to get correct order in queue and include current track
                    _playQueue = new Queue<int>(_playHistory.Reverse());
                    _playQueue.Enqueue(_currentTrack);
                    _playHistory.Clear();
                    PlayTrack(_playQueue.Dequeue());

                    // print for debugging
                    Console.WriteLine($"Current Track: {_currentTrack}");
                    Console.WriteLine($"Queue refilled from history: {string.Join(", ", _playQueue)}");
                    Console.WriteLine($"Queue: {string.Join(", ", _playQueue)}");
                    Console.WriteLine($"History: {string.Join(", ", _playHistory)}");
                }
                else
                {
                    Console.WriteLine("\nEnd of Queue, Stopping Playback.");
                    StopPlayback();
                }
            }
        }

        public void PlayFromBeginning()
        {
            ResetPlayQueueAndHistory();
            PlayTrack(1);
            BuildQueueFromTrack(1);
        }

        public void PlayFromTrack(int trackNumber)
        {
            // TODO:
            // if user selects a track number from queue, we need to jump to it and update the queue and history
            // if user selects a track number not in queue, we need to find it in history and update the queue and history
            // if shuffle mode is enabled, create new shuffle queue from that track
            ResetPlayQueueAndHistory();
            PlayTrack(trackNumber);
            BuildQueueFromTrack(trackNumber);
        }

        private void BuildQueueFromTrack(int trackNumber)
        {
            _playQueue = new Queue<int>(Enumerable.Range(trackNumber + 1, _trackCount - trackNumber));
            _playHistory = new Stack<int>(Enumerable.Range(1, trackNumber - 1));

            Console.WriteLine($"Current Track: {_currentTrack}");
            Console.WriteLine($"Queue: {string.Join(", ", _playQueue)}");
            Console.WriteLine($"History: {string.Join(", ", _playHistory)}");
        }

        private void PlayTrack(int trackNumber)
        {
            if (!IsValidTrack(trackNumber, out string error))
            {
                Console.WriteLine(error);
                return;
            }

            var mediaPath = $"cdda:///{_currentDrive}";
            var media = new Media(_libVlc, mediaPath, FromType.FromLocation);
            media.AddOption($":cdda-track={trackNumber}");

            _currentTrack = trackNumber;
            _mediaPlayer.Play(media);

            Console.WriteLine($"\nPlaying track {_currentTrack} of {_trackCount}");
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
            Console.WriteLine("Playback paused.");
        }

        public void ResumePlayback()
        {
            _mediaPlayer?.Play();
            Console.WriteLine("Playback resumed.");
        }

        public void SkipToNextTrack()
        {
            PlayNextTrack();
        }

        public void SkipToPreviousTrack()
        {
            if (_playHistory.Count == 0)
            {
                Console.WriteLine("No previous track in history.");
                return;
            }

            // TODO: change this to restart current track if more than 5 seconds have elapsed instead of always going to previous track

            // If going back to previous track, we should add current track back to the front of the queue so it can be played again if user goes forward
            _playQueue = new Queue<int>(new[] { _currentTrack }.Concat(_playQueue));

            int previousTrack = _playHistory.Pop();
            PlayTrack(previousTrack);

            Console.WriteLine($"Current Track: {_currentTrack}");
            Console.WriteLine($"Queue: {string.Join(", ", _playQueue)}");
            Console.WriteLine($"History: {string.Join(", ", _playHistory)}");
        }

        public void StopPlayback()
        {
            ResetPlayQueueAndHistory();
            _currentTrack = 0;
            _mediaPlayer?.Stop();
            Console.WriteLine("Playback stopped.");
        }

        public void ChangeVolume(int volume)
        {
            if (volume < MinVolume || volume > MaxVolume)
            {
                Console.WriteLine($"Volume must be between {MinVolume} and {MaxVolume}.");
                return;
            }
            _mediaPlayer.Volume = volume;
            Console.WriteLine($"Volume set to {volume}%.");
        }


        
        public void SeekTo(string seekTimeStr)
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.timespan.tryparse?view=net-10.0
            // If format is "mm:ss", prepend "00:" to make it "00:mm:ss"
            string normalized = seekTimeStr.Split(':').Length == 2
                ? $"00:{seekTimeStr}"
                : seekTimeStr;

            if (!TimeSpan.TryParse(normalized, out TimeSpan seekTime))
            {
                Console.WriteLine("Invalid time format. Use mm:ss or hh:mm:ss");
                return;
            }

            long trackLengthMs = _mediaPlayer.Length;
            long seekMs = (long)seekTime.TotalMilliseconds;

            if (seekMs > trackLengthMs)
            {
                Console.WriteLine($"Cannot seek to {seekTime:mm\\:ss} - track is only {TimeSpan.FromMilliseconds(trackLengthMs):mm\\:ss} long");
                return;
            }

            _mediaPlayer.Time = seekMs;
            Console.WriteLine($"Seeking to {seekTime:mm\\:ss}");
        }


        public int CurrentTrack()
        {
            return _currentTrack;
        }

        public int TrackCount()
        {
            return _trackCount;
        }

        public Queue<int> ShuffleQueue()
        {
            // TODO: take whole tracklist and shuffle, if there is a _currentTrack, put it the rest of the tracks in a queue and play from there
            throw new NotImplementedException();
        }

        public void LogCurrentState()
        {
            Console.WriteLine($"\nCurrent Track: {_currentTrack}");
            Console.WriteLine($"Track Count: {_trackCount}");
            Console.WriteLine($"Media Player State: {_mediaPlayer?.State}");
            Console.WriteLine($"Volume: {_mediaPlayer?.Volume}%");
            // https://stackoverflow.com/questions/68367609/vlc-libvlc-state-t-state-machine
            // if media player state is nothing special or ended, then dont compute these lines:
            if (_mediaPlayer?.State != VLCState.NothingSpecial && _mediaPlayer?.State != VLCState.Ended && _mediaPlayer?.State != VLCState.Stopped)
            {
                Console.WriteLine($"Seekable: {_mediaPlayer?.IsSeekable}");
                Console.WriteLine($"Time (ms): {_mediaPlayer?.Time}");
                Console.WriteLine($"Position (%): {_mediaPlayer?.Position}");
                Console.WriteLine($"Length (ms): {_mediaPlayer?.Length}\n");
            }

        }

        public void SetRepeatMode(int mode)
        {
            // implement repeat mode (3 modes: no repeat, repeat disc, repeat track)
            // TODO: clean this up by using an enum for repeat mode instead of int and removing the if statements
            if (mode < 0 || mode > 2)
            {
                Console.WriteLine("Invalid repeat mode. Valid modes are: 0 (no repeat), 1 (repeat disc), 2 (repeat track).");
                return;
            }
            else if (mode == 0)
            {
                _repeatMode = RepeatMode.NoRepeat;
                Console.WriteLine("Repeat Mode set to No Repeat.");
            }
            else if (mode == 1)
            {
                _repeatMode = RepeatMode.RepeatAll;
                Console.WriteLine("Repeat Mode set to Repeat All.");

            }
            else if (mode == 2)
            {
                _repeatMode = RepeatMode.RepeatTrack;
                Console.WriteLine("Repeat Mode set to Repeat Track.");
            }
        }

        public void SetShuffleMode(bool enabled)
        {
            _shuffleEnabled = enabled;
            if (enabled)
            {
                ShuffleQueue();
                // TODO: if no track is playing, start playing from shuffle queue?
                if (_mediaPlayer?.State != VLCState.Playing)
                {
                    
                }
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
            ResetPlayQueueAndHistory();
            BuildQueueFromTrack(_currentTrack);
        }
    }
}
