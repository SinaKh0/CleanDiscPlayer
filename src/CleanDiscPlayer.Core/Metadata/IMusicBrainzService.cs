using MetaBrainz.MusicBrainz.Interfaces.Entities;

namespace CleanDiscPlayer.Core.Metadata
{
    /// <summary>
    /// Provides metadata lookup services via the MusicBrainz API.
    /// </summary>
    public interface IMusicBrainzService
    {
        /// <summary>
        /// Looks up album and track information for a given disc ID.
        /// If multiple releases are found, returns the first one automatically.
        /// </summary>
        /// <param name="discId">The MusicBrainz disc ID (28-character string).</param>
        /// <returns>Album information including tracks, or error if not found.</returns>
        /// <remarks>
        /// For more control over release selection when multiple releases exist,
        /// use <see cref="LookupDiscWithOptionsAsync"/> instead.
        /// </remarks>
        Task<LookupResult> LookupDiscAsync(string discId);

        /// <summary>
        /// Looks up disc and returns all matching releases if multiple are found.
        /// Caller is responsible for selecting which release to use.
        /// </summary>
        /// <param name="discId">The MusicBrainz disc ID (28-character string).</param>
        /// <returns>
        /// A result containing all matching releases, or error information if lookup failed.
        /// </returns>
        Task<DiscLookupResult> LookupDiscWithOptionsAsync(string discId);

        /// <summary>
        /// Gets full album info for a specific release.
        /// </summary>
        /// <param name="release">The release to process (from LookupDiscWithOptionsAsync).</param>
        /// <param name="discId">The disc ID to match against media in the release.</param>
        /// <returns>Album information including tracks for the matched medium.</returns>
        Task<LookupResult> GetAlbumInfoAsync(IRelease release, string discId);
    }
}
