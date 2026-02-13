using CleanDiscPlayer.Core.Metadata;
using CleanDiscPlayer.WindowsPlayer.Disc;
using CleanDiscPlayer.WindowsPlayer.Playback;
using System.Text;

namespace CleanDiscPlayer.Cli
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // change encoding to UTF-8 to support special characters in metadata and track titles
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            await Run();
        }

        /// <summary>
        /// Starts the CLI loop, waits for user input to play or stop CD playback.
        /// </summary>
        private static async Task Run()
        {
            Console.WriteLine("=== CleanDisc Player CLI ===");
            
            WindowsDiscService discService = new WindowsDiscService();
            MusicBrainzService metadataService = new MusicBrainzService();
            WindowsPlaybackService mediaPlayer = new WindowsPlaybackService();

            var disc = discService.GetDiscInfo();

            if (disc == null)
            {
                Console.WriteLine("No disc found, exiting.");
                return;
            }
            else
            {
                Console.WriteLine($"Drive:     {disc.DevicePath}");
                Console.WriteLine($"Disc ID:   {disc.DiscId}");
                Console.WriteLine($"Duration:  {disc.Duration}");
                Console.WriteLine($"Tracks:    {disc.TrackCount}");
                Console.WriteLine($"TOC ID:    {disc.TOCId}");
                Console.WriteLine($"Track Lengths:");
                foreach (var track in disc.Tracks)
                {
                    Console.WriteLine($"  {track.Number}. {track.Duration:mm\\:ss}");
                }
                Console.WriteLine("");
            }
            //Console.WriteLine("\nPress ENTER to continue...");
            //Console.ReadLine();

            // Look up metadata
            Console.WriteLine("Looking up album info from MusicBrainz...");
            var albumInfo = await metadataService.LookupDiscAsync(disc.DiscId);

            if (albumInfo != null)
            {
                Console.WriteLine($"\nAlbum: {albumInfo.Title}");
                Console.WriteLine($"Media Title: {albumInfo.MediumTitle}");
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
                Console.WriteLine("Album not found in MusicBrainz database.");
            }

            // Create media player and initialize with disc info
            mediaPlayer.Init(disc.DevicePath, disc.TrackCount);

            while (true)
            {
                //Console.Clear();
                Console.WriteLine("\n=== CleanDisc Player CLI ===");
                Console.WriteLine("Commands:");
                Console.WriteLine("  play   - Start playback");
                Console.WriteLine("  play # - Play specified track number");
                Console.WriteLine("  pause  - Resume playback");
                Console.WriteLine("  resume - Skip to next track");
                Console.WriteLine("  prev   - Skip to previous track");
                Console.WriteLine("  next   - Skip to next track");
                Console.WriteLine("  seek t - Seek to specified time in minutes and seconds (mm:ss or hh:mm:ss)");
                Console.WriteLine("  volume - Adjust volume of player (0-100%)");
                Console.WriteLine("  stop   - Stop playback");
                Console.WriteLine("  eject  - Eject disc and exit application");
                Console.WriteLine("  exit   - Exit application");
                Console.WriteLine("  log    - Provides some info on status of player");
                Console.Write("Enter command: ");

                var command = Console.ReadLine()?.Trim().ToLower();
                
                // Handle "play #" before the switch
                if (command.StartsWith("play ") && int.TryParse(command.Split(' ')[1], out int trackNumber))
                {
                    mediaPlayer.PlayTrack(trackNumber);
                }
                else if (command.StartsWith("volume ") && int.TryParse(command.Split(' ')[1], out int volumeNumber))
                {
                    mediaPlayer.ChangeVolume(volumeNumber);
                }
                else if (command.StartsWith("seek "))
                {
                    string seekTimeStr = command.Split(' ')[1]; //command.Substring(5).Trim(); // Get the part after "seek "
                    mediaPlayer.SeekTo(seekTimeStr);
                }
                else
                {
                    switch (command)
                    {
                        case "log":
                            mediaPlayer.LogCurrentState();
                            break;

                        case "play":
                            mediaPlayer.PlayFromBeginning();
                            break;

                        case "resume":
                            mediaPlayer.ResumePlayback();
                            break;

                        case "next":
                            mediaPlayer.SkipToNextTrack();
                            break;

                        case "prev":
                            mediaPlayer.SkipToPreviousTrack();
                            break;

                        case "repeat":
                            // TODO: Implement repeat logic (3 modes: no repeat, repeat disc, repeat track)
                            break;

                        case "shuffle":
                            // TODO: Implement shuffle logic
                            break;

                        case "pause":
                            mediaPlayer.PausePlayback();
                            break;

                        case "stop":
                            mediaPlayer.StopPlayback();
                            break;

                        case "tracklist":
                            // TODO: Implement track listing logic here
                            break;

                        case "eject":
                            mediaPlayer.StopPlayback();
                            discService.EjectDisc(disc.DevicePath);
                            // break; //TODO: listen for disc inserted event and reinitialize player instead of exiting application
                            return;

                        case "exit":
                            mediaPlayer.StopPlayback();
                            return;

                        default:
                            Console.WriteLine("Unknown command. Try 'play', 'stop', or 'exit'.");
                            Console.WriteLine("Press ENTER to continue...");
                            Console.ReadLine();
                            break;
                    }
                }
            }
        }
    }
}