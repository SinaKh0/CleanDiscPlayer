using MetaBrainz.Common;
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
        private RateLimitInfo? _lastRateLimitInfo;

        public MusicBrainzService()
        {
            // Provide user-agent
            _query = new Query("CleanDiscPlayer", new Version(1, 0), "https://github.com/SinaKh0/CleanDiscPlayer");
        }

        public async Task<LookupResult> LookupDiscAsync(string discId)
        {
            try
            {
                // https://musicbrainz.org/doc/MusicBrainz_API

                // https://musicbrainz.org/ws/2/discid/pcgmzmDWsctNXPLxoQsXadhvaLA-
                // https://musicbrainz.org/cdtoc/x92mQ8poBkpI5gLY9PyPa.935Oo-
                // https://musicbrainz.org/ws/2/discid/x92mQ8poBkpI5gLY9PyPa.935Oo-


                // Check if we should wait before making the request
                if (_lastRateLimitInfo.HasValue)
                {
                    var info = _lastRateLimitInfo.Value;
                    if (info.RemainingRequests.HasValue && info.RemainingRequests.Value == 0)
                    {
                        if (info.ResetIn.HasValue)
                        {
                            Console.WriteLine($"Rate limit reached. Waiting {info.ResetIn.Value} seconds...");
                            await Task.Delay(TimeSpan.FromSeconds(info.ResetIn.Value + 1));
                        }
                    }
                }

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
                    return LookupResult.Fail(
                        "This disc was not found in the MusicBrainz database.",
                        LookupErrorType.NotFound
                    );
                }

                Console.WriteLine($"Found {foundDisc.Releases.Count} Release(s).");

                // Get the first release (usually the most relevant)
                var release = foundDisc.Releases.First();


                // If there are multiple releases, let user select the most appropriate one.
                if (foundDisc.Releases.Count > 1)
                {
                    // Display release options to the user
                    Console.WriteLine($"Multiple releases ({foundDisc.Releases.Count}) found for this disc ID:");
                    for (int i = 0; i < foundDisc.Releases.Count; i++)
                    {
                        var r = foundDisc.Releases[i];
                        Console.WriteLine($"{i + 1}. {r.Title} by {GetArtistName(r.ArtistCredit)} ({r.Date?.ToString() ?? "Unknown Date"})");
                    }

                    // Prompt user for selection
                    Console.Write("Select a release (default - 1): ");

                    var command = Console.ReadLine()?.Trim().ToLower();

                    // validate user input and handle invalid selections
                    if (int.TryParse(command, out int selectedIndex) && selectedIndex > 0 && selectedIndex <= foundDisc.Releases.Count)
                    {
                        release = foundDisc.Releases[selectedIndex - 1];
                    }
                    else if (string.IsNullOrEmpty(command)) 
                    {
                        Console.WriteLine("Defaulting to the first release.");
                    }
                    else
                    {
                        Console.WriteLine("Invalid selection. Defaulting to the first release.");
                    }
                }

                Console.WriteLine($"Selected release: {release.Title} by {GetArtistName(release.ArtistCredit)}");

                // THIS IS AN UNNECESSARY EXTRA LOOKUP - THE DISC RESPONSE ALREADY INCLUDES THE TRACKS
                // Fetch full release details with recordings (tracks)
                //var fullRelease = await _query.LookupReleaseAsync(
                //    release.Id,
                //    Include.Recordings | Include.ArtistCredits | Include.DiscIds
                //);

                var albumInfo = MapToAlbumInfo(release, discId);
                return LookupResult.Ok(albumInfo);
            }
            catch (HttpRequestException ex) when (ex.InnerException is System.Security.Authentication.AuthenticationException)
            {
                return LookupResult.Fail(
                    "SSL connection failed. Please check your system certificates and internet connection.",
                    LookupErrorType.SslError
                );
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                return LookupResult.Fail(
                    "MusicBrainz rate limit exceeded. Please wait a moment and try again.",
                    LookupErrorType.RateLimited
                );
            }
            catch (HttpError ex) when (ex.Status == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                // 503 - Rate limiting
                // The HttpError object has all the response details
                Console.WriteLine("=== RATE LIMIT HIT ===");
                Console.WriteLine($"Status: {ex.Status}");
                Console.WriteLine($"Reason: {ex.Reason}");
                Console.WriteLine($"Content: {ex.Content}");

                if (ex.ResponseHeaders != null)
                {
                    var info = new RateLimitInfo(ex.ResponseHeaders);
                    Console.WriteLine($"Allowed: {info.AllowedRequests}");
                    Console.WriteLine($"Remaining: {info.RemainingRequests}");
                    Console.WriteLine($"Reset In: {info.ResetIn} seconds");
                    Console.WriteLine($"Reset At: {info.ResetAt}");
                }
                Console.WriteLine("======================");

                // Extract rate limit info from error response headers if available
                if (ex.ResponseHeaders != null)
                {
                    var rateLimitInfo = new RateLimitInfo(ex.ResponseHeaders);

                    var message = "Rate limit exceeded.";
                    if (rateLimitInfo.ResetIn.HasValue)
                    {
                        message += $" Try again in {rateLimitInfo.ResetIn.Value} seconds.";
                    }
                    else if (rateLimitInfo.ResetAt.HasValue)
                    {
                        message += $" Try again after {rateLimitInfo.ResetAt.Value:HH:mm:ss}.";
                    }

                    return LookupResult.Fail(message, LookupErrorType.RateLimited);
                }

                return LookupResult.Fail(
                    "Rate limit exceeded. Please wait before trying again.",
                    LookupErrorType.RateLimited
                );
            }
            catch (HttpError ex) when (ex.Status == System.Net.HttpStatusCode.NotFound)
            {
                return LookupResult.Fail(
                    "Disc ID not found in MusicBrainz database.",
                    LookupErrorType.NotFound
                );
            }
            catch (HttpError ex)
            {
                // All other HTTP errors
                var message = $"HTTP {(int)ex.Status} ({ex.Status})";
                if (ex.Reason != null)
                    message += $": {ex.Reason}";
                if (ex.Content != null)
                    message += $"\n{ex.Content}";

                return LookupResult.Fail(message, LookupErrorType.NetworkError);
            }
            catch (TaskCanceledException)
            {
                return LookupResult.Fail(
                    "Request timed out. Please check your internet connection.",
                    LookupErrorType.Timeout
                );
            }
            catch (HttpRequestException ex)
            {
                return LookupResult.Fail(
                    $"Network error: {ex.Message}",
                    LookupErrorType.NetworkError
                );
            }
            catch (Exception ex)
            {
                return LookupResult.Fail(
                    $"Unexpected error: {ex.Message}",
                    LookupErrorType.Unknown
                );
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
