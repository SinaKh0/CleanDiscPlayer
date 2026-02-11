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
        private int _currentTrack = 1;
        private int _trackCount = 0;

        private const int MinVolume = 0;
        private const int MaxVolume = 100;

        public void Init(string driveLetter, int trackCount)
        {
            if (_initialized)
                throw new InvalidOperationException("Already initialized");

            _initialized = true;
            _currentDrive = driveLetter.TrimEnd('\\').TrimEnd('/');
            _trackCount = trackCount;

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
        /// Called automatically when the current track ends.
        /// </summary>
        private void AdvanceTrack()
        {
            if (_currentTrack < _trackCount)
            {
                PlayTrack(_currentTrack + 1);
                Console.Write("Enter command: ");
            }
            else
            {
                Console.WriteLine("\nEnd of disc.");
                this.StopPlayback();
                Console.Write("Enter command: ");
            }
        }

        public void PlayFromBeginning()
        {
            PlayTrack(1);
        }

        public void PlayTrack(int trackNumber)
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
            if (_currentTrack >= _trackCount)
            {
                Console.WriteLine("Already on last track.");
                return;
            }
            PlayTrack(++_currentTrack);
            Console.WriteLine($"Track {_currentTrack} of {_trackCount}");
        }

        public void SkipToPreviousTrack()
        {
            if (_currentTrack <= 1)
            {
                // TODO: change this to restart current track instead of doing nothing
                // if on shuffle mode, change this to go to previously played track instead of previous track in tracklist
                Console.WriteLine("Already on first track.");
                return;
            }
            // TODO: change this to restart current track if more than 5 seconds have elapsed instead of always going to previous track
            PlayTrack(--_currentTrack);
            Console.WriteLine($"Track {_currentTrack} of {_trackCount}");
        }

        public void StopPlayback()
        {
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

        public void RepeatMode(int mode)
        {
            // TODO: implement repeat mode (3 modes: no repeat, repeat disc, repeat track)
            throw new NotImplementedException();
        }

        public void ShuffleMode(bool enabled)
        {
            // TODO: implement shuffle mode
            throw new NotImplementedException();
        }
    }
}
