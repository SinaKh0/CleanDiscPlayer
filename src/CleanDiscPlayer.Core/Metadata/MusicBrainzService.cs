using MetaBrainz.Common;
using MetaBrainz.MusicBrainz;
using MetaBrainz.MusicBrainz.Interfaces.Entities;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;

namespace CleanDiscPlayer.Core.Metadata
{
    /// <summary>
    /// Provides metadata retrieval from the MusicBrainz web service.
    /// </summary>
    public class MusicBrainzService : IMusicBrainzService
    {
        private readonly Query _query;
        private readonly ILogger<MusicBrainzService> _logger;
        private RateLimitInfo? _lastRateLimitInfo;

        /// <summary>
        /// Initializes a new instance of MusicBrainzService.
        /// </summary>
        /// <param name="logger">Logger for diagnostic and error information.</param>
        public MusicBrainzService(ILogger<MusicBrainzService> logger)
        {
            // Provide user-agent
            _logger = logger;
            _query = new Query("CleanDiscPlayer", new Version(0, 5), "https://github.com/SinaKh0/CleanDiscPlayer");
            _logger.LogDebug("MusicBrainzService initialized");
        }

        /// <summary>
        /// Looks up disc and returns all matching releases.
        /// </summary>
        public async Task<DiscLookupResult> LookupDiscWithOptionsAsync(string discId)
        {
            const int maxRetries = 3;
            int attempt = 0;

            while (attempt < maxRetries)
            {
                attempt++;

                try
                {
                    _logger.LogDebug("Disc lookup attempt {Attempt}/{MaxRetries} for: {DiscId}", attempt, maxRetries, discId);
                    return await LookupDiscWithOptionsInternalAsync(discId);
                }
                catch (HttpRequestException ex) when (ex.InnerException is System.IO.IOException || ex.InnerException is System.Net.Sockets.SocketException || ex.InnerException is System.Security.Authentication.AuthenticationException)
                {
                    _logger.LogWarning("Network error on attempt {Attempt}/{MaxRetries}: {Message}", attempt, maxRetries, ex.Message);

                    if (attempt >= maxRetries)
                    {
                        _logger.LogError("All retry attempts failed");
                        return DiscLookupResult.Fail(
                            "Network connection failed after multiple attempts. Please check your internet connection.",
                            LookupErrorType.NetworkError
                        );
                    }

                    // Exponential backoff: 1s, 2s, 4s
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    _logger.LogInformation("Retrying in {Delay} seconds...", delay.TotalSeconds);
                    await Task.Delay(delay);
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogWarning("Request timeout on attempt {Attempt}/{MaxRetries}",
                        attempt, maxRetries);

                    if (attempt >= maxRetries)
                    {
                        _logger.LogError("All retry attempts failed due to timeout");
                        return DiscLookupResult.Fail(
                            "Request timed out after multiple attempts. Please check your internet connection.",
                            LookupErrorType.Timeout
                        );
                    }

                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    _logger.LogInformation("Retrying in {Delay} seconds...", delay.TotalSeconds);
                    await Task.Delay(delay);
                }
            }
            // Should never reach here due to maxRetries check, but compiler needs it
            _logger.LogError("Unexpected: Exited retry loop without returning");
            return DiscLookupResult.Fail("Unexpected retry logic error", LookupErrorType.Unknown);
        }

        /// <summary>
        /// Internal method that performs the actual lookup (renamed from LookupDiscWithOptionsAsync).
        /// </summary>
        private async Task<DiscLookupResult> LookupDiscWithOptionsInternalAsync(string discId)
        {
            try
            {
                // https://musicbrainz.org/doc/MusicBrainz_API

                // https://musicbrainz.org/ws/2/discid/pcgmzmDWsctNXPLxoQsXadhvaLA-
                // https://musicbrainz.org/cdtoc/x92mQ8poBkpI5gLY9PyPa.935Oo-
                // https://musicbrainz.org/ws/2/discid/x92mQ8poBkpI5gLY9PyPa.935Oo-

                _logger.LogDebug("Starting disc lookup for: {DiscId}", discId);

                // Check rate limit before request
                await HandleRateLimitAsync();

                // Look up the disc by its ID
                var disc = await _query.LookupDiscIdAsync(
                    discId,
                    toc: null,
                    inc: Include.Artists |
                         Include.Recordings |
                         Include.ReleaseGroups |
                         Include.Labels,
                    allMediaFormats: true,
                    noStubs: true
                );

                var foundDisc = disc.Disc;

                // DEBUG: See raw disc object
                //var discJson = JsonSerializer.Serialize(disc, new JsonSerializerOptions { WriteIndented = true });
                //Console.WriteLine("=== RAW DISC RESPONSE ===");
                //Console.WriteLine(discJson);

                if (foundDisc?.Releases == null || !foundDisc.Releases.Any())
                {
                    _logger.LogWarning("No releases found for disc ID: {DiscId}", discId);
                    return DiscLookupResult.Fail(
                        "This disc was not found in the MusicBrainz database.",
                        LookupErrorType.NotFound
                    );
                }

                _logger.LogInformation("Found {ReleaseCount} release(s) for disc ID: {DiscId}", foundDisc.Releases.Count, discId);

                // Map to ReleaseOption objects
                var releaseOptions = foundDisc.Releases.Select(r => new ReleaseOption(
                    release: r,
                    title: r.Title ?? "Unknown Album",
                    artist: GetArtistName(r.ArtistCredit),
                    date: r.Date?.ToString() ?? "Unknown Date"
                )).ToList();

                return DiscLookupResult.Ok(releaseOptions);
            }
            catch (HttpError ex) when (ex.Status == System.Net.HttpStatusCode.ServiceUnavailable || ex.Status == System.Net.HttpStatusCode.TooManyRequests)
            {
                // 503 - Rate limiting
                // The HttpError object has all the response details
                _logger.LogWarning("Rate limit hit: {Status} - {Reason}", ex.Status, ex.Reason);
                return HandleRateLimitError(ex);
            }
            catch (HttpError ex) when (ex.Status == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Disc not found (404): {DiscId}", discId);
                return DiscLookupResult.Fail(
                    "Disc ID not found in MusicBrainz database.",
                    LookupErrorType.NotFound
                );
            }
            catch (HttpError ex)
            {
                _logger.LogError("HTTP error during lookup: {Status} - {Reason} - {Content}", ex.Status, ex.Reason, ex.Content);

                var message = $"HTTP {(int)ex.Status} ({ex.Status})";
                if (ex.Reason != null)
                    message += $": {ex.Reason}";

                return DiscLookupResult.Fail(message, LookupErrorType.NetworkError);
            }
        }

        /// <summary>
        /// Looks up album and track information for a given disc ID.
        /// Automatically selects the first release if multiple are found.
        /// </summary>
        public async Task<LookupResult> LookupDiscAsync(string discId)
        {
            _logger.LogInformation("Looking up disc ID: {DiscId}", discId);

            var discResult = await LookupDiscWithOptionsAsync(discId);

            if (!discResult.Success)
            {
                return LookupResult.Fail(discResult.ErrorMessage!, discResult.ErrorType);
            }

            if (discResult.Releases == null || !discResult.Releases.Any())
            {
                return LookupResult.Fail("No releases found", LookupErrorType.NotFound);
            }

            // Automatically select first release
            var selectedRelease = discResult.Releases.First();
            _logger.LogInformation("Auto-selected first release: {Title} by {Artist}",
                selectedRelease.Title, selectedRelease.Artist);

            return await GetAlbumInfoAsync(selectedRelease.Release, discId);
        }

        /// <summary>
        /// Gets full album info for a specific release.
        /// </summary>
        public async Task<LookupResult> GetAlbumInfoAsync(IRelease release, string discId)
        {
            try
            {
                _logger.LogDebug("Processing release: {Title} by {Artist}", release.Title, GetArtistName(release.ArtistCredit));

                var albumInfo = MapToAlbumInfo(release, discId);

                _logger.LogInformation("Successfully mapped album info: {Title}, {TrackCount} tracks", albumInfo.Title, albumInfo.Tracks.Count);

                return LookupResult.Ok(albumInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping album info for release: {ReleaseId}", release.Id);
                return LookupResult.Fail(
                    $"Error processing release: {ex.Message}",
                    LookupErrorType.Unknown
                );
            }
        }

        /// <summary>
        /// Handles rate limit checking and waiting if necessary.
        /// </summary>
        private async Task HandleRateLimitAsync()
        {
            if (!_lastRateLimitInfo.HasValue)
                return;

            var info = _lastRateLimitInfo.Value;
            if (info.RemainingRequests.HasValue && info.RemainingRequests.Value == 0)
            {
                if (info.ResetIn.HasValue)
                {
                    var waitSeconds = info.ResetIn.Value + 1;
                    _logger.LogWarning("Rate limit reached. Waiting {WaitSeconds} seconds...", waitSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
                }
            }
        }

        /// <summary>
        /// Handles rate limit errors and extracts timing information.
        /// </summary>
        private DiscLookupResult HandleRateLimitError(HttpError ex)
        {
            if (ex.ResponseHeaders != null)
            {
                var rateLimitInfo = new RateLimitInfo(ex.ResponseHeaders);
                _lastRateLimitInfo = rateLimitInfo;

                _logger.LogDebug("Rate limit info - Allowed: {Allowed}, Remaining: {Remaining}, Reset in: {ResetIn}s", rateLimitInfo.AllowedRequests, rateLimitInfo.RemainingRequests, rateLimitInfo.ResetIn);

                var message = "Rate limit exceeded.";
                if (rateLimitInfo.ResetIn.HasValue)
                {
                    message += $" Try again in {rateLimitInfo.ResetIn.Value} seconds.";
                }
                else if (rateLimitInfo.ResetAt.HasValue)
                {
                    message += $" Try again after {rateLimitInfo.ResetAt.Value:HH:mm:ss}.";
                }

                return DiscLookupResult.Fail(message, LookupErrorType.RateLimited);
            }

            return DiscLookupResult.Fail(
                "Rate limit exceeded. Please wait before trying again.",
                LookupErrorType.RateLimited
            );
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

            if (release.Media == null || !release.Media.Any())
            {
                _logger.LogWarning("Release {ReleaseId} has no media", release.Id);
                return albumInfo;
            }

            _logger.LogDebug("Release has {MediaCount} medium(s)", release.Media.Count);

            IMedium? matchedMedium = null;

            // Find the medium that matches our disc ID
            foreach (var medium in release.Media)
            {
                if (medium.Tracks == null)
                    continue;

                var mediumDiscId = medium.Discs?.FirstOrDefault()?.Id.ToString();
                _logger.LogDebug("Medium {Position}: {Title} (Disc ID: {DiscId}, {TrackCount} tracks)",
                    medium.Position, medium.Title ?? "Untitled", mediumDiscId, medium.TrackCount);

                // Check if this medium matches our disc ID
                if (medium.Discs != null && medium.Discs.Any(d => d.Id.ToString() == discId))
                {
                    _logger.LogInformation("Found matching medium at position {Position} for disc ID: {DiscId}",
                        medium.Position, discId);
                    matchedMedium = medium;
                    albumInfo.MediumTitle = medium.Title ?? albumInfo.Title;
                    break;
                }
            }

            // Fallback to first medium if no match found
            if (matchedMedium == null)
            {
                matchedMedium = release.Media?.First();
                _logger.LogWarning("No matching medium found for disc ID {DiscId}, using first medium", discId);
            }

            // Map tracks from the matched medium
            if (matchedMedium?.Tracks != null)
            {
                foreach (var track in matchedMedium.Tracks)
                {
                    albumInfo.Tracks.Add(new TrackMetadata
                    {
                        Position = track.Position ?? 0,
                        Title = track.Title ?? "Unknown Track",
                        Artist = GetArtistName(track.ArtistCredit) ?? albumInfo.Artist,
                        Length = track.Length.HasValue ? track.Length.Value : null
                    });
                }

                _logger.LogDebug("Mapped {TrackCount} tracks from medium", albumInfo.Tracks.Count);
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

        public void Dispose()
        {
            _query?.Dispose();
            _logger.LogDebug("MusicBrainzService disposed");
        }
    }
}
