namespace CleanDiscPlayer.Core.Disc
{

    /// <summary>
    /// Represents information about a CD, including all tracks, identifying data, and active disc drive path.
    /// </summary>
    public class DiscInfo
    {
        /// <summary>
        /// Device letter of the CD drive (not OS-specific: "D:\", or "/dev/cdrom", ...)
        /// </summary>
        public string DevicePath { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier for the disc
        /// </summary>
        public string DiscId { get; set; } = string.Empty;

        /// <summary>
        /// Alternative identifier based on the Table of Contents of the disc
        /// </summary>
        public string TOCId { get; set; } = string.Empty;

        /// <summary>
        /// Identifier used by the FreeDB database
        /// </summary>
        public string FreeDbId { get; set; } = string.Empty;

        /// <summary>
        /// Catalog number of the media (if available) COULD REMOVE?
        /// </summary>
        public string MediaCatalogueNumber { get; set; } = string.Empty;

        /// <summary>
        /// Total duration of the disc as TimeSpan
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Total length of the disc in sectors
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Number of tracks on the disc
        /// </summary>
        public int TrackCount { get; set; }

        /// <summary>
        /// Array of Track objects representing each track on the disc
        /// </summary>
        public TrackInfo[] Tracks { get; set; } = Array.Empty<TrackInfo>(); 
    }
}
