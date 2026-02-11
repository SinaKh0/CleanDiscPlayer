namespace CleanDiscPlayer.Core.Playback
{
    /// <summary>
    /// Defines disc playback operations
    /// </summary>
    public interface IPlaybackService
    {
        /// <summary>
        /// Initializes LibVLC and input variables.
        /// Example driveLetter: "Z:\\" 
        /// </summary>
        /// <param name="drivePath"></param>
        /// <param name="trackCount"></param>
        public void Init(string drivePath, int trackCount);

        /// <summary>
        /// Plays the audio track at the specified track number.
        /// </summary>
        /// <param name="trackNumber">The one-based index of the track to play. Must be within the range of available tracks.</param>
        public void PlayTrack(int trackNumber);

        /// <summary>
        /// Starts playback from the beginning of the cd.
        /// </summary>
        public void PlayFromBeginning();

        /// <summary>
        /// Stops audio playback if it is currently in progress.
        /// </summary>
        public void StopPlayback();
    }
}
