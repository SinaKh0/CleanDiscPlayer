using MetaBrainz.MusicBrainz.Interfaces.Entities;

namespace CleanDiscPlayer.Core.Metadata
{
    /// <summary>
    /// Result of a disc lookup operation that may return multiple release options.
    /// </summary>
    public class DiscLookupResult
    {
        /// <summary>
        /// Indicates whether the lookup was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// List of release options found for the disc ID.
        /// Null if lookup failed or no releases found.
        /// </summary>
        public List<ReleaseOption>? Releases { get; set; }

        /// <summary>
        /// Error message if the lookup failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Type of error that occurred during lookup.
        /// </summary>
        public LookupErrorType ErrorType { get; set; }

        /// <summary>
        /// Creates a successful result with release options.
        /// </summary>
        public static DiscLookupResult Ok(List<ReleaseOption> releases) => new()
        {
            Success = true,
            Releases = releases
        };

        /// <summary>
        /// Creates a failed result with error information.
        /// </summary>
        public static DiscLookupResult Fail(string message, LookupErrorType type) => new()
        {
            Success = false,
            ErrorMessage = message,
            ErrorType = type
        };
    }

    /// <summary>
    /// Represents a single release option from MusicBrainz.
    /// </summary>
    public class ReleaseOption
    {
        /// <summary>
        /// The full release entity from MusicBrainz API.
        /// </summary>
        public IRelease Release { get; set; }

        /// <summary>
        /// Album/release title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Primary artist name.
        /// </summary>
        public string Artist { get; set; }

        /// <summary>
        /// Release date as a string.
        /// </summary>
        public string Date { get; set; }

        public ReleaseOption(IRelease release, string title, string artist, string date)
        {
            Release = release;
            Title = title;
            Artist = artist;
            Date = date;
        }
    }
}