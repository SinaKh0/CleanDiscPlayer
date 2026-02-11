namespace CleanDiscPlayer.Core.Disc
{
    /// <summary>
    /// Defines disc information retrieval operations
    /// </summary>
    public interface IDiscService
    {
        /// <summary>
        /// Retrieves information about the currently loaded disc, if available.
        /// </summary>
        /// <returns>A <see cref="DiscInfo"/> object containing details about the loaded disc, or <see langword="null"/> if no
        /// disc is present.</returns>
        DiscInfo? GetDiscInfo();

        /// <summary>
        /// Ejects the currently loaded disc from the drive.
        /// </summary>
        /// <remarks>If no disc is present in the drive, this method has no effect.</remarks>
        /// <param name="drivePath">CD drive path, example: "D" in Windows</param>
        void EjectDisc(string drivePath);
    }
}
