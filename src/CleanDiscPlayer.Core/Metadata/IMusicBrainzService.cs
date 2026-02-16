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
        /// <returns>Lookup Result that can either be the album information including tracks, or error details.</returns>
        Task<LookupResult> LookupDiscAsync(string discId);
    }
}
