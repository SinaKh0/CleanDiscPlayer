using MetaBrainz.MusicBrainz.DiscId;

namespace CleanDiscPlayer.WindowsPlayer.Disc
{
    public static class DiscIdTest
    {
        public static void Run()
        {
            var drives = FindCdDrives();
            var drive = FindCdDrive();

            if (drives == null)
            {
                Console.WriteLine("No Disc Drives found.");
                return;
            }

            Console.WriteLine($"Found disc drive: {string.Join(", ", drives)}");

            if (drive == null)
            {
                Console.WriteLine("No audio CD found.");
                return;
            }

            Console.WriteLine($"Found CD drive: {drive}");
            


            var defaultDevice = TableOfContents.DefaultDevice;
            var availableDevices = TableOfContents.AvailableDevices;
            var availableFeatures = TableOfContents.AvailableFeatures;
            var defaultPort = TableOfContents.DefaultPort;
            var defaultUrlScheme = TableOfContents.DefaultUrlScheme;
            var defaultWebSite = TableOfContents.DefaultWebSite;

            Console.WriteLine("\n");
            Console.WriteLine($"Available devices: {string.Join(", ", availableDevices)}");
            Console.WriteLine($"Available features: {string.Join(", ", availableFeatures)}");
            Console.WriteLine($"DiscId CD drive: {defaultDevice}");
            Console.WriteLine($"Default port: {defaultPort}");
            Console.WriteLine($"Default URL scheme: {defaultUrlScheme}");
            Console.WriteLine($"Default website: {defaultWebSite}");
            Console.WriteLine("\n");

            try
            {
                // Read the disc (basic — just TOC)
                var toc = TableOfContents.ReadDisc(defaultDevice);
                
                Console.WriteLine("Disc TOC:");
                Console.WriteLine($"Device Name:     {toc.DeviceName}");
                Console.WriteLine($"Disc ID:         {toc.DiscId}");
                Console.WriteLine($"FreeDB ID:       {toc.FreeDbId}");
                Console.WriteLine($"First track:     {toc.FirstTrack}");
                Console.WriteLine($"Last track:      {toc.LastTrack}");
                Console.WriteLine($"Length:          {toc.Length}");
                Console.WriteLine($"Media Cat Num:   {toc.MediaCatalogNumber}");
                Console.WriteLine($"Port:            {toc.Port}");
                Console.WriteLine($"Submission URL:  {toc.SubmissionUrl}");
                Console.WriteLine($"Text Info:       {toc.TextInfo}");
                Console.WriteLine($"Text Languages:  {toc.TextLanguages}");
                Console.WriteLine($"Tracks:          {toc.Tracks}");
                Console.WriteLine($"URL Scheme:      {toc.UrlScheme}");
                Console.WriteLine($"Website:         {toc.WebSite}");

                foreach (var track in toc.Tracks)
                {
                    Console.WriteLine("\n");
                    Console.WriteLine($"Track {track.Number}:");
                    Console.WriteLine($"  Duration:     {track.Duration}");
                    Console.WriteLine($"  ISRC:         {track.Isrc}");
                    Console.WriteLine($"  Length:       {track.Length}");
                    Console.WriteLine($"  Number:       {track.Number}");
                    Console.WriteLine($"  Offset:       {track.Offset}");
                    Console.WriteLine($"  Start Time:   {track.StartTime}");
                    Console.WriteLine($"  Text Info:    {track.TextInfo}");
                }

                Console.WriteLine("\n");
                Console.WriteLine($"Track Count:      {toc.Tracks.Count}");
                Console.WriteLine($"Choose Track number to display details (or press ENTER to skip):");
                
                var input = Console.ReadLine(); // check for integer and valid track number

                Console.WriteLine($"Track {input} details:");
                Console.WriteLine($"  Length: {toc.Tracks[int.Parse(input)].Length}");


            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to read disc: {ex.Message}");
            }
        }

        private static string? FindCdDrive()
        {
            var drives = new List<string>();

            foreach (var drive in DriveInfo.GetDrives())
            {
                drives.Add(drive.Name);
                if (drive.DriveType == DriveType.CDRom && drive.IsReady)
                {
                    return drive.Name;
                }
            }

            return null;
        }

        public static List<string> FindCdDrives()
        {
            var drives = new List<string>();

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.CDRom && drive.IsReady)
                {
                    drives.Add(drive.Name);
                }
            }

            return drives;
        }
    }
}

