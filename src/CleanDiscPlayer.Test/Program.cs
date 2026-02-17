using CleanDiscPlayer.Core.Logging;
using CleanDiscPlayer.Core.Metadata;
using MetaBrainz.MusicBrainz.Interfaces.Entities;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Text;

namespace CleanDiscPlayer.Tests
{
    /// <summary>
    /// Provides a set of manual test cases for verifying MusicBrainz lookup functionality during development.
    /// </summary>
    /// <remarks>This class is intended for internal use only and is not part of the public API surface. It
    /// contains entry points and test scenarios for manually exercising MusicBrainz service lookups, including various
    /// disc IDs and edge cases. The tests are designed to be run interactively by developers and are not suitable for
    /// automated testing environments.</remarks>
    internal class ManualTests
    {

        private static async Task Main(string[] args)
        {
            // change encoding to UTF-8 to support special characters in metadata and track titles
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            await TestMusicBrainzLookups();
        }

        private static async Task TestMusicBrainzLookups()
        {
            // Create logger for the service
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                // Configure Serilog
                var config = new LoggingConfiguration
                {
                    ConsoleLevel = LogLevel.Warning,
                    EnableConsoleLogging = true,
                    EnableFileLogging = false
                };
                builder.ConfigureAppLogging(config);
            });


            var logger = loggerFactory.CreateLogger<MusicBrainzService>();
            var service = new MusicBrainzService(logger);

            // To open the test cases in MusicBrainz, use the following URL format:
            // Website: https://musicbrainz.org/cdtoc/<discId>          ex: https://musicbrainz.org/cdtoc/x92mQ8poBkpI5gLY9PyPa.935Oo-
            // API:     https://musicbrainz.org/ws/2/discid/<discId>    ex: https://musicbrainz.org/ws/2/discid/x92mQ8poBkpI5gLY9PyPa.935Oo-
            var testCases = new Dictionary<string, string>
            {
                ["Single-Release, Single-Disc"] = "pcgmzmDWsctNXPLxoQsXadhvaLA-",
                ["Single-Release, Multi-Disc (disc 1)"] = "vgzLd4iFtOzL3xP_df.sfqzAhsE-",
                ["Single-Release, Multi-Disc (disc 2)"] = "x92mQ8poBkpI5gLY9PyPa.935Oo-",
                ["Multi-Release, Different Releases"] = "lwHl8fGzJyLXQR33ug60E8jhf4k-",
                ["Multi-Release, Same Release Different Editions"] = "XzPS7vW.HPHsYemQh0HBUGr8vuU-",
                ["No Release (SHOULD FAIL)"] = "xZPMdJzUsP8qqCIySSo_AgBKNH4-",
                ["Invalid DiscId (SHOULD FAIL)"] = "INVALID_ID_123"
            };

            foreach (var (name, discId) in testCases)
            {
                Console.WriteLine($"\n=== TESTING: {name} ===");
                //var result = await service.LookupDiscAsync(discId);

                // Use LookupDiscWithOptionsAsync to see all releases
                var discResult = await service.LookupDiscWithOptionsAsync(discId);

                if (discResult.Success && discResult.Releases != null)
                {
                    if (discResult.Releases.Count == 1)
                    {
                        // Single release - get full album info
                        var release = discResult.Releases[0];
                        var albumResult = await service.GetAlbumInfoAsync(release.Release, discId);

                        if (albumResult.Success)
                        {
                            var albumInfo = albumResult.Album!;
                            Console.WriteLine($"✓ Found: {albumInfo.Title} by {albumInfo.Artist} ({albumInfo.ReleaseDate}) with {albumInfo.Tracks.Count} track(s).");
                        }
                        else
                        {
                            Console.WriteLine($"✗ Failed to get album info: {albumResult.ErrorMessage}");
                        }
                    }
                    else
                    {
                        // Multiple releases - just show them
                        Console.WriteLine($"✓ Found {discResult.Releases.Count} release(s):");
                        foreach (var release in discResult.Releases)
                        {
                            Console.WriteLine($"   - {release.Title} by {release.Artist} ({release.Date})");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"✗ Lookup failed: {discResult.ErrorMessage}");

                    switch (discResult.ErrorType)
                    {
                        case LookupErrorType.NotFound:
                            Console.WriteLine("   Album not found in MusicBrainz database.");
                            break;
                        case LookupErrorType.RateLimited:
                            Console.WriteLine("   Tip: Wait at least 1 second between requests.");
                            break;
                        case LookupErrorType.SslError:
                            Console.WriteLine("   Try: Check your system date/time.");
                            break;
                    }
                }

                await Task.Delay(1000); // Rate limit
            }

            await TestRateLimiting(logger);

            service.Dispose();
        }


        public static async Task TestRateLimiting(ILogger<MusicBrainzService> logger)
        {
            var service = new MusicBrainzService(logger);
            var testDiscId = "pcgmzmDWsctNXPLxoQsXadhvaLA-";

            Console.WriteLine("\n=== TESTING RATE LIMITING ===");
            Console.WriteLine("Making rapid requests to trigger rate limit...\n");

            for (int i = 1; i <= 5; i++)
            {
                Console.WriteLine($"Request {i} at {DateTime.Now:HH:mm:ss.fff}");

                var result = await service.LookupDiscAsync(testDiscId);

                if (result.Success)
                {
                    Console.WriteLine($"  ✓ Success: {result.Album?.Title}");
                }
                else
                {
                    Console.WriteLine($"  ✗ Failed: {result.ErrorMessage}");
                    Console.WriteLine($"  Error Type: {result.ErrorType}");

                    if (result.ErrorType == LookupErrorType.RateLimited)
                    {
                        Console.WriteLine("  *** RATE LIMIT TRIGGERED ***");
                        break;
                    }
                }

                // Don't wait - send immediately to trigger rate limit
            }
            service.Dispose();
        }
    }
}