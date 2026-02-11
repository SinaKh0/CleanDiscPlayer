namespace CleanDiscPlayer.Core.Disc
{

    /// <summary>
    /// Represents a single track on a CD.
    /// </summary>
    public class TrackInfo
    {
        /// <summary>
        /// Track number on CD
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// Track duration as TimeSpan
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Track start time as TimeSpan
        /// </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>
        /// Track length in sectors
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Start offset in sectors (CD TOC)
        /// </summary>
        public int Offset { get; set; } 
    }
}