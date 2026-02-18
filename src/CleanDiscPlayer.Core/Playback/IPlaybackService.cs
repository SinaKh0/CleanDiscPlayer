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

        ///// <summary>
        ///// Plays the audio track at the specified track number.
        ///// </summary>
        ///// <param name="trackNumber">The one-based index of the track to play. Must be within the range of available tracks.</param>
        //public void PlayTrack(int trackNumber);

        /// <summary>
        /// Starts playback from the beginning of the cd.
        /// </summary>
        public void PlayFromBeginning();

        /// <summary>
        /// Starts playback from beginning of queue
        /// </summary>
        public void PlayFromBeginningOfQueue();

        /// <summary>
        /// Starts playback beginning from the specified track number.
        /// </summary>
        /// <param name="trackNumber">The 1-based index of the track to start playback from. Must be within the range of available tracks.</param>
        public void PlayFromTrack(int trackNumber);

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
        /// Sets the playback volume to the specified level.
        /// </summary>
        /// <param name="volume">The desired volume level. Valid values are typically in the range 0 (mute) to 100 (maximum volume).</param>
        public void ChangeVolume(int volume);

        /// <summary>
        /// Seeks to the specified position in the media, as indicated by the provided time string.
        /// </summary>
        /// <param name="seekTimeStr">A string representing the target position to seek to, typically formatted as a time value (for example,
        /// "00:01:30" for 1 minute and 30 seconds).</param>
        public void SeekTo(string seekTimeStr);

        /// <summary>
        /// Returns the current track number (1-based).
        /// </summary>
        public int CurrentTrack();

        /// <summary>
        /// Returns the total number of tracks on the disc.
        /// </summary>
        public int TrackCount();

        /// <summary>
        /// Logs the current state of the object for debugging or monitoring purposes.
        /// </summary>
        public void LogCurrentState();

        /// <summary>
        /// Sets the repeat mode for playback.
        /// </summary>
        /// <param name="mode">An integer value that specifies the repeat mode to apply (3 modes: no repeat, repeat disc, repeat track).</param>
        public void SetRepeatMode(int mode);

        /// <summary>
        /// Enables or disables shuffle mode for playback.
        /// </summary>
        /// <param name="enabled">true to enable shuffle mode; false to disable it.</param>
        public void SetShuffleMode(bool enabled);

        /// <summary>
        /// Shuffles the play queue, randomizing the order of tracks to be played next.
        /// </summary>
        /// <remarks>If no track is currently playing, all tracks are included in the shuffled queue. If a
        /// track is currently playing, only the remaining tracks are shuffled and added to the queue. The play history
        /// is reset as part of this operation.</remarks>
        public void ShuffleQueue();

        /// <summary>
        /// Returns the current playing track's progress information
        /// </summary>
        public (TimeSpan Time, TimeSpan Length, float Position, int Volume, bool ShuffleMode, string CurrentRepeatMode) GetCurrentTrackProgress();
    }
}
