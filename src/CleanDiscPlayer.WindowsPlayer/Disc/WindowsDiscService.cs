using System.Runtime.InteropServices;
using CleanDiscPlayer.Core.Disc;
using MetaBrainz.MusicBrainz.DiscId;

namespace CleanDiscPlayer.WindowsPlayer.Disc
{
    public class WindowsDiscService : IDiscService
    {
        public DiscInfo? GetDiscInfo()
        {
            var drive = FindCdDrive();

            if (drive == null)
            {
                Console.WriteLine("No CD drive found.");
                return null;
            }

            try
            {
                var defaultDevice = TableOfContents.DefaultDevice;
                var toc = TableOfContents.ReadDisc(defaultDevice);

                var discInfo = new DiscInfo
                {
                    DevicePath = toc.DeviceName,
                    DiscId = toc.DiscId,
                    TOCId = toc.SubmissionUrl.ToString(), // TODO: implement by extracting toc from url
                    FreeDbId = toc.FreeDbId,
                    MediaCatalogueNumber = toc.MediaCatalogNumber,
                    Length = toc.Length,
                    TrackCount = toc.LastTrack - toc.FirstTrack + 1,
                    Tracks = new TrackInfo[toc.LastTrack - toc.FirstTrack + 1]
                };

                for (int i = toc.FirstTrack; i <= toc.LastTrack; i++)
                {
                    var trackInfo = toc.Tracks[i];
                    discInfo.Tracks[i - toc.FirstTrack] = new TrackInfo
                    {
                        Number = trackInfo.Number,
                        Duration = trackInfo.Duration,
                        StartTime = trackInfo.StartTime,
                        Length = trackInfo.Length,
                        Offset = trackInfo.Offset
                    };
                }

                discInfo.Duration = TimeSpan.FromSeconds((toc.Length - toc.Tracks[1].Offset) / 75.0); // 75 sectors per second
                return discInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving disc info: {ex.Message}");
                return null;
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

        private static List<string> FindCdDrives()
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

        /// <summary>
        /// Sends a command string to the Windows Media Control Interface (MCI).
        /// Used to control multimedia devices such as CD/DVD drives.
        /// </summary>
        /// <param name="command">The MCI command string to execute (e.g. "set cdrom door open").</param>
        /// <param name="returnValue">Buffer to receive the return string from MCI. Pass null if not needed.</param>
        /// <param name="returnLength">Size of the return buffer. Pass 0 if returnValue is null.</param>
        /// <param name="callback">Handle to a window to receive MCI notifications. Pass IntPtr.Zero if not needed.</param>
        /// <returns>Zero if successful, otherwise an MCI error code.</returns>
        [DllImport("winmm.dll")]
        static extern int mciSendString(string command, string? returnValue, int returnLength, IntPtr callback);

        public void EjectDisc(string driveLetter)
        {
            try
            {
                // Normalize input, accept "Z", "Z:", or "Z:\"
                string drive = driveLetter.TrimEnd('\\').TrimEnd(':') + ":";

                Console.WriteLine($"Ejecting disc from drive {drive}");
                mciSendString($"open {drive} type cdaudio alias cdrom", null, 0, IntPtr.Zero);
                mciSendString("set cdrom door open", null, 0, IntPtr.Zero);
                mciSendString("close cdrom", null, 0, IntPtr.Zero);
                Console.WriteLine("Eject command sent.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ejecting disc: {ex.Message}");
            }
        }
    }
}
