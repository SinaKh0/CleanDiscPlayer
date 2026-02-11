using CleanDiscPlayer.WindowsPlayer.Disc;
using CleanDiscPlayer.WindowsPlayer.Playback;

namespace CleanDiscPlayer.Cli
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Run();
        }

        /// <summary>
        /// Starts the CLI loop, waits for user input to play or stop CD playback.
        /// </summary>
        private static void Run()
        {
            Console.WriteLine("=== CleanDisc Player CLI ===");
            
            WindowsDiscService discService = new WindowsDiscService();

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
            }
            Console.WriteLine("\nPress ENTER to continue...");
            Console.ReadLine();

            WindowsPlaybackService mediaPlayer = new WindowsPlaybackService();
            mediaPlayer.Init(disc.DevicePath, disc.TrackCount);

            while (true)
            {
                //Console.Clear();
                Console.WriteLine("\n=== CleanDisc Player CLI ===");
                Console.WriteLine("Commands:");
                Console.WriteLine("  play   - Start playback");
                Console.WriteLine("  play # - Play specified track number");
                Console.WriteLine("  stop   - Stop playback");
                Console.WriteLine("  eject  - Eject disc and exit application");
                Console.WriteLine("  exit   - Exit application");
                Console.Write("Enter command: ");

                var command = Console.ReadLine()?.Trim().ToLower();
                
                // Handle "play #" before the switch
                if (command.StartsWith("play ") && int.TryParse(command.Split(' ')[1], out int trackNumber))
                {
                    mediaPlayer.PlayTrack(trackNumber);
                }
                else
                {
                    switch (command)
                    {
                        case "play":
                            mediaPlayer.PlayFromBeginning();
                            break;

                        case "next":
                            // TODO: Implement skip next track logic here
                            break;

                        case "prev":
                            // TODO: Implement skip prev track logic here
                            break;

                        case "skip":
                            // TODO: Implement skip to time position on track logic here
                            break;

                        case "repeat":
                            // TODO: Implement repeat logic (2 modes: repeat disc, repeat track) here
                            break;

                        case "shuffle":
                            // TODO: Implement shuffle logic here
                            break;

                        case "volume":
                            // TODO: Implement volume adjustment logic here
                            break;

                        case "pause":
                            // TODO: Implement disc pause logic here
                            break;

                        case "stop":
                            mediaPlayer.StopPlayback();
                            break;

                        case "tracklist":
                            // TODO: Implement track listing logic here
                            break;

                        case "eject":
                            mediaPlayer.StopPlayback(); // Ensure playback is stopped before ejecting
                            discService.EjectDisc(disc.DevicePath);
                            //break; // No need to break here since we want to end the loop with exit after ejecting
                            return;

                        case "exit":
                            mediaPlayer.StopPlayback(); // Ensure playback is stopped before exiting
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