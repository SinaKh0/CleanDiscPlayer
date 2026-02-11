namespace CleanDiscPlayer.Core.Metadata
{
    /// <summary>
    /// Provides metadata lookup services via the MusicBrainz API.
    /// </summary>
    public interface IMusicBrainzService
    {
        /// <summary>
        /// Looks up album and track information for a given disc ID.
        /// </summary>
        /// <param name="discId">The MusicBrainz disc ID (28-character string).</param>
        /// <returns>Album information including tracks, or null if not found.</returns>
        Task<AlbumInfo?> LookupDiscAsync(string discId);
    }
}
