using CleanDiscPlayer.Core.Disc;
using CleanDiscPlayer.Core.Logging;
using CleanDiscPlayer.Core.Metadata;
using CleanDiscPlayer.Core.Playback;
using CleanDiscPlayer.WindowsPlayer.Disc;
using CleanDiscPlayer.WindowsPlayer.Playback;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Text;

namespace CleanDiscPlayer.Cli
{
    internal class Program
    {
        /// <summary>
        /// Entry point for the CleanDisc Player CLI application.
        /// </summary>
        /// <param name="args">Command-line arguments for configuring logging and behavior.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task Main(string[] args)
        {
            // change encoding to UTF-8 to support special characters in metadata and track titles
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            // parse CLI arguments and convert to logging configuration
            var options = CliOptions.Parse(args);

            // Show help if requested
            if (options.ShowHelp)
            {
                CliOptions.ShowHelpText();
                return;  // Exit after showing help
            }

            var loggingConfig = options.ToLoggingConfiguration();

            // Set up dependency injection container
            var services = new ServiceCollection();

            // Configure logging using shared setup from Core
            services.AddLogging(builder =>
            {
                builder.ConfigureAppLogging(loggingConfig);
            });

            // Register application services with DI container
            services.AddSingleton<IDiscService, WindowsDiscService>();
            services.AddSingleton<IMusicBrainzService, MusicBrainzService>();
            services.AddSingleton<IPlaybackService, WindowsPlaybackService>();

            // Build the service provider to resolve dependencies
            var serviceProvider = services.BuildServiceProvider();

            // Get logger for Program class
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("CleanDisc Player CLI starting...");

            // Run the main application logic
            await Run(
                serviceProvider.GetRequiredService<IDiscService>(),
                serviceProvider.GetRequiredService<IMusicBrainzService>(),
                serviceProvider.GetRequiredService<IPlaybackService>(),
                serviceProvider.GetRequiredService<ILogger<Program>>()
            );

            logger.LogInformation("CleanDisc Player CLI shutting down.");

            // Clean up and dispose of services
            await serviceProvider.DisposeAsync();
        }

        /// <summary>
        /// Main application logic: reads disc, fetches metadata, and starts command loop.
        /// </summary>
        /// <param name="discService">Service for reading disc information and controlling drive.</param>
        /// <param name="metadataService">Service for fetching album/track metadata from MusicBrainz.</param>
        /// <param name="mediaPlayer">Service for controlling audio playback.</param>
        /// <param name="logger">Logger instance for the Program class.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task Run(IDiscService discService, IMusicBrainzService metadataService, IPlaybackService mediaPlayer, ILogger<Program> logger)
        {
            Console.WriteLine("=== CleanDisc Player CLI ===");
            logger.LogDebug("Initializing disc service...");

            // Read disc information from drive
            var disc = discService.GetDiscInfo();

            if (disc == null)
            {
                logger.LogWarning("No disc found in drive");
                Console.WriteLine("No disc found, exiting.");
                //TODO: listen for disc inserted event and reinitialize player instead of exiting application
                return;
            }

            logger.LogInformation("Disc found: {DiscId}, {TrackCount} tracks", disc.DiscId, disc.TrackCount);

            // Display disc information to user
            Console.WriteLine($"Drive:     {disc.DevicePath}");
            Console.WriteLine($"Disc ID:   {disc.DiscId}");
            Console.WriteLine($"Duration:  {disc.Duration}");
            Console.WriteLine($"Tracks:    {disc.TrackCount}");
            //Console.WriteLine($"TOC:       {disc.TOCId}");
            //Console.WriteLine($"Track Lengths:");
            //foreach (var track in disc.Tracks)
            //{
            //    Console.WriteLine($"  {track.Number}. {track.Duration:mm\\:ss}");
            //}
            Console.WriteLine("");

            // Look up album metadata from MusicBrainz
            logger.LogDebug("Looking up metadata from MusicBrainz...");
            Console.WriteLine("Looking up album info from MusicBrainz...");
            var discResult = await metadataService.LookupDiscWithOptionsAsync(disc.DiscId);

            LookupResult result;

            if (!discResult.Success)
            {
                // Lookup failed - create error result
                logger.LogWarning("Metadata lookup failed: {ErrorType} - {ErrorMessage}",
                    discResult.ErrorType, discResult.ErrorMessage);

                Console.WriteLine($"Lookup failed: {discResult.ErrorMessage}");

                // Provide user-friendly error messages based on error type
                switch (discResult.ErrorType)
                {
                    case LookupErrorType.NotFound:
                        Console.WriteLine("Album not found in MusicBrainz database.");
                        Console.WriteLine($"You can submit this disc at: {disc.TOCId}");
                        break;
                    case LookupErrorType.RateLimited:
                        Console.WriteLine("Tip: Wait at least 1 second between requests.");
                        break;
                    case LookupErrorType.SslError:
                        Console.WriteLine("Try: Check your system date/time.");
                        break;
                }

                // Create a failed result to pass to command loop
                result = LookupResult.Fail(discResult.ErrorMessage!, discResult.ErrorType);
            }
            else
            {
                // Lookup succeeded - handle release selection
                ReleaseOption selectedRelease;

                if (discResult.Releases!.Count == 1)
                {
                    // Only one release - auto-select it
                    selectedRelease = discResult.Releases[0];
                    logger.LogInformation("Single release found: {Title} by {Artist}",
                        selectedRelease.Title, selectedRelease.Artist);
                }
                else
                {
                    // Multiple releases - let user choose
                    logger.LogInformation("Multiple releases found ({Count}), prompting user for selection",
                        discResult.Releases.Count);

                    Console.WriteLine($"\nMultiple releases ({discResult.Releases.Count}) found for this disc:");
                    for (int i = 0; i < discResult.Releases.Count; i++)
                    {
                        var r = discResult.Releases[i];
                        Console.WriteLine($"  {i + 1}. {r.Title} by {r.Artist} ({r.Date})");
                    }

                    Console.Write("\nSelect a release (1-{0}, default: 1): ", discResult.Releases.Count);
                    var input = Console.ReadLine()?.Trim();

                    // Validate user input
                    if (string.IsNullOrEmpty(input))
                    {
                        selectedRelease = discResult.Releases[0];
                        logger.LogInformation("User accepted default (first release)");
                        Console.WriteLine("Using first release.");
                    }
                    else if (int.TryParse(input, out int selectedIndex) &&
                             selectedIndex >= 1 &&
                             selectedIndex <= discResult.Releases.Count)
                    {
                        selectedRelease = discResult.Releases[selectedIndex - 1];
                        logger.LogInformation("User selected release {Index}: {Title}",
                            selectedIndex, selectedRelease.Title);
                    }
                    else
                    {
                        selectedRelease = discResult.Releases[0];
                        logger.LogWarning("Invalid selection '{Input}', defaulting to first release", input);
                        Console.WriteLine("Invalid selection. Using first release.");
                    }
                }

                // Get full album info for the selected release
                Console.WriteLine($"Selected: {selectedRelease.Title} by {selectedRelease.Artist}");
                logger.LogDebug("Fetching full album info for selected release...");

                result = await metadataService.GetAlbumInfoAsync(selectedRelease.Release, disc.DiscId);

                if (result.Success && result.Album != null)
                {
                    var albumInfo = result.Album;
                    logger.LogInformation("Album info retrieved: {Title}, {TrackCount} tracks",
                        albumInfo.Title, albumInfo.Tracks.Count);

                    // Display album and track metadata
                    Console.WriteLine($"\nAlbum: {albumInfo.Title}");
                    if (!string.IsNullOrEmpty(albumInfo.MediumTitle) && albumInfo.MediumTitle != albumInfo.Title)
                    {
                        Console.WriteLine($"Media Title: {albumInfo.MediumTitle}");
                    }
                    Console.WriteLine($"Artist: {albumInfo.Artist}");
                    Console.WriteLine($"Release Date: {albumInfo.ReleaseDate}");
                    Console.WriteLine($"\nTracks:");
                    foreach (var track in albumInfo.Tracks)
                    {
                        Console.WriteLine($"  {track.Position}. {track.Title} - {track.Artist} ({track.Length:mm\\:ss})");
                    }
                }
                else
                {
                    logger.LogError("Failed to retrieve album info: {ErrorMessage}", result.ErrorMessage);
                    Console.WriteLine($"Error getting album info: {result.ErrorMessage}");
                }
            }

            // Initialize playback service with disc information
            logger.LogDebug("Initializing playback service...");
            mediaPlayer.Init(disc.DevicePath, disc.TrackCount);

            Console.WriteLine("\nType 'help' for commands");

            // Start interactive command loop
            await CommandLoop(mediaPlayer, discService, result, disc, logger);
        }

        /// <summary>
        /// Interactive command loop that processes user input for playback control.
        /// </summary>
        /// <param name="mediaPlayer">Service for controlling audio playback.</param>
        /// <param name="discService">Service for disc operations like ejecting.</param>
        /// <param name="result">Metadata lookup result containing album/track information.</param>
        /// <param name="disc">Disc information from the physical CD.</param>
        /// <param name="logger">Logger instance for the Program class.</param>
        /// <returns>A task representing the asynchronous operation. Returns when user exits.</returns>
        private static async Task CommandLoop(IPlaybackService mediaPlayer, IDiscService discService, LookupResult result, DiscInfo disc, ILogger<Program> logger)
        {
            while (true)
            {
                // Display command menu
                //Console.WriteLine("\n=== CleanDisc Player CLI ===");
                //Console.WriteLine("Commands:");
                //Console.WriteLine("  play        - Start playback from first track");
                //Console.WriteLine("  play #      - Play specified track number");
                //Console.WriteLine("  shuffleplay - Play in shuffle mode from random track");
                //Console.WriteLine("  pause       - Pase playback");
                //Console.WriteLine("  resume      - Resume playback");
                //Console.WriteLine("  prev        - Skip to previous track");
                //Console.WriteLine("  next        - Skip to next track");
                //Console.WriteLine("  seek t      - Seek to specified time in minutes and seconds (mm:ss or hh:mm:ss)");
                //Console.WriteLine("  repeat m    - Set repeat mode (0: no repeat, 1: repeat all, 2: repeat track)");
                //Console.WriteLine("  shuffle m   - Toggle shuffle mode (0: off, 1: on)");
                //Console.WriteLine("  tracklist   - Show track listing and metadata");
                //Console.WriteLine("  volume      - Adjust volume of player (0-100%)");
                //Console.WriteLine("  stop        - Stop playback");
                //Console.WriteLine("  eject       - Eject disc and exit application");
                //Console.WriteLine("  exit        - Exit application");
                //Console.WriteLine("  log         - Provides some info on status of player");
                Console.Write("> ");//Console.Write("Enter command: ");

                var command = Console.ReadLine()?.Trim().ToLower();


                if (string.IsNullOrEmpty(command))
                    continue;

                logger.LogDebug("User command: {Command}", command);

                // Handle parameterized commands before switch statement
                if (command.StartsWith("play ") && int.TryParse(command.Split(' ')[1], out int trackNumber))
                {
                    logger.LogInformation("User requested track {TrackNumber}", trackNumber);
                    mediaPlayer.PlayFromTrack(trackNumber);
                    LogTrackInfo(mediaPlayer, result);
                    continue;
                }

                // Handle "volume #" - set volume level
                if (command.StartsWith("volume ") && int.TryParse(command.Split(' ')[1], out int volume))
                {
                    logger.LogInformation("User set volume to {Volume}", volume);
                    mediaPlayer.ChangeVolume(volume);
                    continue;
                }

                // Handle "seek t" - seek to timestamp
                if (command.StartsWith("seek "))
                {
                    string seekTime = command.Substring(5);
                    logger.LogDebug("User seeking to {SeekTime}", seekTime);
                    mediaPlayer.SeekTo(seekTime);
                    continue;
                }
                // Handle "repeat m" - set repeat mode
                if (command.StartsWith("repeat "))
                {
                    string modeStr = command.Split(' ')[1];
                    if (int.TryParse(modeStr, out int modeNumber))
                    {
                        logger.LogInformation("User set repeat mode to {RepeatMode}", modeNumber);
                        mediaPlayer.SetRepeatMode(modeNumber);
                    }
                    else
                    {
                        logger.LogWarning("Invalid repeat mode argument: {ModeStr}", modeStr);
                        Console.WriteLine("Invalid repeat mode. Use 0 for no repeat, 1 for repeat all, or 2 for repeat track.");
                        Console.WriteLine("Press ENTER to continue...");
                        Console.ReadLine();
                    }
                    continue;
                }
                // Handle "shuffle m" - toggle shuffle mode
                if (command.StartsWith("shuffle "))
                {
                    string shuffleStr = command.Split(' ')[1];
                    if (int.TryParse(shuffleStr, out int shuffleNumber))
                    {
                        logger.LogInformation("User set shuffle mode to {ShuffleEnabled}", shuffleNumber == 1);
                        mediaPlayer.SetShuffleMode(shuffleNumber == 1);
                    }
                    else
                    {
                        logger.LogWarning("Invalid shuffle mode argument: {ShuffleStr}", shuffleStr);
                        Console.WriteLine("Invalid shuffle mode. Use 0 for off or 1 for on.");
                        Console.WriteLine("Press ENTER to continue...");
                        Console.ReadLine();
                    }
                    continue;
                }
                
                switch (command)
                {
                    case "log":
                        LogTrackInfo(mediaPlayer, result);
                        mediaPlayer.LogCurrentState();
                        break;

                    case "play":
                        logger.LogInformation("Starting playback from beginning");
                        mediaPlayer.PlayFromBeginning();
                        LogTrackInfo(mediaPlayer, result);
                        break;

                    case "shuffleplay":
                        logger.LogInformation("Starting shuffle play");
                        mediaPlayer.SetShuffleMode(true);
                        mediaPlayer.PlayFromBeginningOfQueue();
                        LogTrackInfo(mediaPlayer, result);
                        break;

                    case "resume":
                        logger.LogInformation("Resuming playback");
                        mediaPlayer.ResumePlayback();
                        LogTrackInfo(mediaPlayer, result);
                        break;

                    case "next":
                        logger.LogInformation("Skipping to next track");
                        mediaPlayer.SkipToNextTrack();
                        LogTrackInfo(mediaPlayer, result);
                        break;

                    case "prev":
                        logger.LogInformation("Skipping to previous track");
                        mediaPlayer.SkipToPreviousTrack();
                        LogTrackInfo(mediaPlayer, result);
                        break;

                    case "shuffle":
                        logger.LogInformation("Shuffling queue");
                        mediaPlayer.SetShuffleMode(true);
                        break;

                    case "pause":
                        logger.LogInformation("Pausing playback");
                        mediaPlayer.PausePlayback();
                        break;

                    case "stop":
                        logger.LogInformation("Stopping playback");
                        mediaPlayer.StopPlayback();
                        break;

                    case "status":
                        logger.LogInformation("Printing Progress Bar");
                        ShowDetailedStatus(mediaPlayer, result);
                        break;

                    case "help":
                        logger.LogInformation("Printing Available Commands");
                        ShowCommands();
                        break;

                    case "tracklist":
                        if (result.Album?.Tracks != null)
                        {
                            Console.WriteLine($"Tracks:");
                            foreach (var track in result.Album.Tracks)
                            {
                                Console.WriteLine($"  {track.Position}. {track.Title} - {track.Artist} ({track.Length:mm\\:ss})");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Tracklist not available.");
                        }
                        break;

                    case "eject":
                        logger.LogInformation("Ejecting disc");
                        mediaPlayer.StopPlayback();
                        discService.EjectDisc(disc.DevicePath);
                        // break; //TODO: listen for disc inserted event and reinitialize player instead of exiting application
                        return;

                    case "exit":
                        logger.LogInformation("Application exiting");
                        mediaPlayer.StopPlayback();
                        return;

                    default:
                        logger.LogDebug("Unknown command: {Command}", command);
                        Console.WriteLine("Unknown command. Try 'play', 'stop', or 'exit'.");
                        Console.WriteLine("Press ENTER to continue...");
                        Console.ReadLine();
                        break;
                }
                
            }
        }

        /// <summary>
        /// Writes all available commands to console
        /// </summary>
        private static void ShowCommands()
        {
            // Display command menu
            Console.WriteLine("");
            Console.WriteLine("Commands:");
            Console.WriteLine("  play        - Start playback from first track");
            Console.WriteLine("  play #      - Play specified track number");
            Console.WriteLine("  shuffleplay - Play in shuffle mode from random track");
            Console.WriteLine("  pause       - Pase playback");
            Console.WriteLine("  resume      - Resume playback");
            Console.WriteLine("  prev        - Skip to previous track");
            Console.WriteLine("  next        - Skip to next track");
            Console.WriteLine("  seek t      - Seek to specified time in minutes and seconds (mm:ss or hh:mm:ss)");
            Console.WriteLine("  repeat m    - Set repeat mode (0: no repeat, 1: repeat all, 2: repeat track)");
            Console.WriteLine("  shuffle m   - Toggle shuffle mode (0: off, 1: on)");
            Console.WriteLine("  tracklist   - Show track listing and metadata");
            Console.WriteLine("  volume      - Adjust volume of player (0-100%)");
            Console.WriteLine("  stop        - Stop playback");
            Console.WriteLine("  eject       - Eject disc and exit application");
            Console.WriteLine("  exit        - Exit application");
            Console.WriteLine("  log         - Provides some info on status of player");
            Console.WriteLine("  status      - Provides info on track playback status");
            Console.WriteLine("");
        }

        private static void ShowDetailedStatus(IPlaybackService mediaPlayer, LookupResult result)
        {
            var (current, total, position, volume, shuffleMode, repeatMode) = mediaPlayer.GetCurrentTrackProgress();
            int curr = mediaPlayer.CurrentTrack();

            if (result.Album?.Tracks == null || curr < 1 || curr > mediaPlayer.TrackCount())
            {
                Console.WriteLine("No media playing or queued to play.");
                return;
            }

            var track = result.Album.Tracks[curr];

            Console.WriteLine($"\n♪ {track.Title} - {track.Artist}");
            Console.WriteLine($"  Track {curr + 1} of {mediaPlayer.TrackCount()}");

            // Simple progress bar
            int barWidth = 50;
            float progress = position;
            int filled = (int)(barWidth * progress);

            Console.Write("  [");
            Console.Write(new string('█', filled));
            Console.Write(new string('-', barWidth - filled));
            Console.WriteLine($"] {current:mm\\:ss} / {total:mm\\:ss}");
            Console.WriteLine($"  Volume: {volume}% | Shuffle: {(shuffleMode ? "ON" : "OFF")} | Repeat: {repeatMode}");
            Console.WriteLine("");
        }


        /// <summary>
        /// Displays information about the currently playing track to the console.
        /// </summary>
        /// <param name="mediaPlayer">Playback service to query current track number.</param>
        /// <param name="result">Metadata lookup result containing track information.</param>
        /// <remarks>
        /// If the current track index is invalid or metadata is unavailable,
        /// displays "Unknown" instead of track details.
        /// </remarks>
        private static void LogTrackInfo(IPlaybackService mediaPlayer, LookupResult result)
        {
            int curr = mediaPlayer.CurrentTrack();

            // Validate track index and metadata availability
            if (curr < 0 || result.Album?.Tracks == null || curr >= result.Album.Tracks.Count)
            {
                Console.WriteLine("");
                Console.WriteLine("♪ Now playing: Unknown");
                Console.WriteLine("");
                return;
            }

            var track = result.Album.Tracks[curr];
            Console.WriteLine("");
            Console.WriteLine($"♪ Now playing: {curr}. {track.Title} - {track.Artist} ({track.Length:mm\\:ss})");
            Console.WriteLine("");
        }
    }
}