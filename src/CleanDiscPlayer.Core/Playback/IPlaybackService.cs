namespace CleanDiscPlayer.Core.Playback
{
    /// <summary>
    /// Defines disc playback operations
    /// </summary>
    public interface IPlaybackService
    {
        /// <summary>
        /// Initializes LibVLC, input variables, and event for continuing to next track when current track ends.
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
        /// Pauses the current playback if it is in progress.
        /// </summary>
        /// <remarks>Call this method to temporarily halt playback without resetting the current position.
        /// Use a corresponding resume method to continue playback from the paused position. If playback is
        /// already paused or stopped, this method has no effect.</remarks>
        public void PausePlayback();

        /// <summary>
        /// Resumes playback if it is currently paused.
        /// </summary>
        /// <remarks>If playback is not paused, calling this method has no effect. This method does not
        /// restart playback if it has already completed or has not been started.</remarks>
        public void ResumePlayback();

        /// <summary>
        /// Skips playback to the next track.
        /// </summary>
        /// <remarks>If the current track is the last in the tracklist, playback remains unchanged.
        /// If playback is already at the last track, will have no effect.
        /// Calling this method when no track is currently playing also has no effect.
        /// </remarks>
        public void SkipToNextTrack();

        /// <summary>
        /// Skips playback to the previous track.
        /// </summary>
        /// <remarks>If the current track is the first in the tracklist, playback remains unchanged.
        /// Calling this method when no track is currently playing also has no effect.
        /// </remarks>
        public void SkipToPreviousTrack();

        /// <summary>
        /// Stops audio playback if it is currently in progress.
        /// </summary>
        public void StopPlayback();

        /// <summary>
        /// Returns the current track number (1-based).
        /// </summary>
        public int CurrentTrack();

        /// <summary>
        /// Returns the total number of tracks on the disc.
        /// </summary>
        public int TrackCount();
    }
}
