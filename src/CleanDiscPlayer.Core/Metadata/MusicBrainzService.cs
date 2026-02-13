using MetaBrainz.MusicBrainz;
using MetaBrainz.MusicBrainz.Interfaces.Entities;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CleanDiscPlayer.Core.Metadata
{
    /// <summary>
    /// Provides metadata retrieval from the MusicBrainz web service.
    /// </summary>
    public class MusicBrainzService : IMusicBrainzService
    {
        private readonly Query _query;

        public MusicBrainzService()
        {
            // Provide user-agent
            _query = new Query("CleanDiscPlayer", new Version(1, 0), "https://github.com/SinaKh0/CleanDiscPlayer");
        }

        public async Task<AlbumInfo?> LookupDiscAsync(string discId)
        {
            try
            {
                // https://musicbrainz.org/doc/MusicBrainz_API

                // https://musicbrainz.org/ws/2/discid/pcgmzmDWsctNXPLxoQsXadhvaLA-
                // https://musicbrainz.org/cdtoc/x92mQ8poBkpI5gLY9PyPa.935Oo-
                // https://musicbrainz.org/ws/2/discid/x92mQ8poBkpI5gLY9PyPa.935Oo-
                // Look up the disc by its ID
                var disc = await _query.LookupDiscIdAsync(
                    discId,
                    toc: null,
                    inc: Include.Artists |
                         Include.Recordings |
                         Include.ReleaseGroups,
                    allMediaFormats: true,
                    noStubs: true
                );

                var foundDisc = disc.Disc;

                //Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(foundDisc, new JsonSerializerOptions { WriteIndented = true }));
                // DEBUG: See raw disc object
                //var discJson = JsonSerializer.Serialize(disc, new JsonSerializerOptions { WriteIndented = true });
                //Console.WriteLine("=== RAW DISC RESPONSE ===");
                //Console.WriteLine(discJson);

                if (foundDisc?.Releases == null || !foundDisc.Releases.Any())
                {
                    return null; // Disc not found in MusicBrainz
                }

                Console.WriteLine($"Found {foundDisc.Releases.Count} Release(s).");

                // Get the first release (usually the most relevant)
                // TODO: Let user choose if multiple releases are found
                var release = foundDisc.Releases.First();

                Console.WriteLine($"Selected first release: {release.Title} by {GetArtistName(release.ArtistCredit)}");

                // Fetch full release details with recordings (tracks)
                var fullRelease = await _query.LookupReleaseAsync(
                    release.Id,
                    Include.Recordings | Include.ArtistCredits | Include.DiscIds
                );

                return MapToAlbumInfo(fullRelease, discId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MusicBrainz lookup failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Maps the specified release to an AlbumInfo object containing album and track metadata.
        /// </summary>
        /// <remarks>If the release contains multiple media (discs), only the tracks from the first medium
        /// are included in the AlbumInfo. Track and album fields with missing data are assigned default value "Unknown Track".</remarks>
        /// <param name="release">The release to map to album information. Cannot be null.</param>
        /// <returns>An AlbumInfo object populated with metadata from the specified release. Default values are used for missing
        /// or null fields.</returns>
        private AlbumInfo MapToAlbumInfo(IRelease release, String discId)
        {
            var albumInfo = new AlbumInfo
            {
                Title = release.Title ?? "Unknown Album",
                Artist = GetArtistName(release.ArtistCredit),
                ReleaseDate = release.Date?.ToString() ?? "Unknown",
                MusicBrainzId = release.Id.ToString()
            };

            // Get tracks from the first medium (disc)
            var medium = release.Media?.FirstOrDefault();

            Console.WriteLine($"Release has {release.Media?.Count ?? 0} medium(s).");

            for (var mediumIndex = 0; mediumIndex < release.Media?.Count; mediumIndex++)
            {
                var currentMedium = release.Media[mediumIndex];
                if (currentMedium.Tracks != null)
                {
                    Console.WriteLine($"Medium {mediumIndex + 1}. {currentMedium.Title}. \n    With {currentMedium.TrackCount} track(s) and disc id: {currentMedium.Discs?.FirstOrDefault()?.Id}.");

                    // DEBUG: See raw media object
                    //var discJson = JsonSerializer.Serialize(medium, new JsonSerializerOptions { WriteIndented = true });
                    //Console.WriteLine("=== RAW DISC RESPONSE ===");
                    //Console.WriteLine(discJson);

                    if (currentMedium.Discs != null && currentMedium.Discs.Any(d => d.Id.ToString() == discId))
                    {
                        Console.WriteLine($"Found matching medium for disc ID {discId} on medium {mediumIndex + 1}.");
                        medium = currentMedium;
                        albumInfo.MediumTitle = currentMedium.Title ?? albumInfo.Title;
                        break;
                    }
                }
            }

            if (medium?.Tracks != null)
            {
                foreach (var track in medium.Tracks)
                {
                    albumInfo.Tracks.Add(new TrackMetadata
                    {
                        Position = track.Position ?? 0,
                        Title = track.Title ?? "Unknown Track",
                        Artist = GetArtistName(track.ArtistCredit) ?? albumInfo.Artist,
                        Length = track.Length.HasValue ? track.Length.Value : null
                    });
                }
            }

            return albumInfo;
        }

        /// <summary>
        /// Generates a display name for an artist or group based on the provided artist credits.
        /// </summary>
        /// <remarks>The returned name concatenates individual artist names with " & " as a separator. If
        /// an artist entry is missing or null, "Unknown" is used in its place.</remarks>
        /// <param name="artistCredit">A read-only list of artist credit entries used to construct the artist name. If null or empty, the method
        /// returns "Unknown Artist".</param>
        /// <returns>A string representing the combined artist name. Returns "Unknown Artist" if no valid artist credits are
        /// provided.</returns>
        private string GetArtistName(IReadOnlyList<INameCredit>? artistCredit)
        {
            if (artistCredit == null || !artistCredit.Any())
                return "Unknown Artist";

            return string.Join(" & ", artistCredit.Select(ac => ac.Artist?.Name ?? "Unknown"));
        }
    }
}
