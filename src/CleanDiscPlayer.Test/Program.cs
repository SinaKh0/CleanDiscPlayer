using CleanDiscPlayer.Core.Metadata;
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
            var service = new MusicBrainzService();

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
                ["No Release"] = "xZPMdJzUsP8qqCIySSo_AgBKNH4-",
                ["Invalid DiscId"] = "INVALID_ID_123"
            };

            foreach (var (name, discId) in testCases)
            {
                Console.WriteLine($"\n=== TESTING: {name} ===");
                var result = await service.LookupDiscAsync(discId);

                if (result != null)
                    Console.WriteLine($"✓ Found: {result.Title} by {result.Artist} ({result.ReleaseDate}) with {result.Tracks.Count} track(s).");
                else
                    Console.WriteLine("✗ Not found");

                await Task.Delay(1000); // Rate limit
            }
        }
    }
}