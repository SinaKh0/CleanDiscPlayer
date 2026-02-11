using CleanDiscPlayer.WindowsPlayer.Disc;

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
            Console.ReadLine();

            discService.EjectDisc("Z"); // FIXME: Replace with actual drive letter from disc info
            Console.ReadLine();

            // inside Main or Run()
            DiscIdTest.Run();
            Console.ReadLine();

            Console.WriteLine("CleanDisc Player CLI");
            Console.WriteLine("Press ENTER to start playback of disc...");
            Console.ReadLine(); // FIXME: Replace with proper argument parsing

            // TODO: Implement disc playback logic here 

            Console.WriteLine("Playing. Press ENTER to stop...");
            Console.ReadLine();

            // TODO: Implement disc stop logic here

            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== CleanDisc Player CLI ===");
                Console.WriteLine("Commands:");
                Console.WriteLine("  play   - Start playback");
                Console.WriteLine("  stop   - Stop playback");
                Console.WriteLine("  exit   - Exit application");
                Console.WriteLine("  other commands...");
                Console.Write("Enter command: ");

                var command = Console.ReadLine()?.Trim().ToLower();

                switch (command)
                {
                    case "play":
                        // TODO: Implement disc playback logic here 
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
                        // TODO: Implement disc stop logic here
                        break;

                    case "tracklist":
                        // TODO: Implement track listing logic here
                        break;

                    case "eject":
                        // TODO: Implement disc eject logic here
                        break;

                    case "exit":
                        // TODO: Stop CD and Clean up resources if necessary
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