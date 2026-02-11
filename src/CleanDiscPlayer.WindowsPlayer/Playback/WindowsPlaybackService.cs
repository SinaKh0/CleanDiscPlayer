using LibVLCSharp.Shared;
using CleanDiscPlayer.Core.Playback;

namespace CleanDiscPlayer.WindowsPlayer.Playback
{
    public class WindowsPlaybackService : IPlaybackService
    {
        private LibVLC _libVlc;
        private MediaPlayer _mediaPlayer;
        private string _currentDrive;
        private int _currentTrack = 1;
        private int _trackCount = 0;

        public void Init(string driveLetter, int trackCount)
        {
            _currentDrive = driveLetter.TrimEnd('\\').TrimEnd('/');
            _trackCount = trackCount;

            LibVLCSharp.Shared.Core.Initialize();

            // Verbose logging for debugging
            //_libVlc = new LibVLC("--verbose=2");
            //_libVlc.Log += (sender, e) => Console.WriteLine($"[VLC] {e.Level}: {e.Message}");

            _libVlc = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVlc);
        }

        public void PlayFromBeginning()
        {
            PlayTrack(1);
        }

        public void PlayTrack(int trackNumber)
        {
            if (trackNumber < 1 || trackNumber > _trackCount)
            {
                Console.WriteLine($"Invalid track. Disc has {_trackCount} tracks.");
                return;
            }

            var mediaPath = $"cdda:///{_currentDrive}";
            var media = new Media(_libVlc, mediaPath, FromType.FromLocation);
            media.AddOption($":cdda-track={trackNumber}");
            _currentTrack = trackNumber;
            _mediaPlayer.Play(media);
            Console.WriteLine($"Playing track {_currentTrack} of {_trackCount}");
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

        public int CurrentTrack()
        {
            return _currentTrack;
        }

        public int TrackCount()
        {
            return _trackCount;
        }

    }
}
