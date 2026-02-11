namespace CleanDiscPlayer.Core.Metadata
{
    /// <summary>
    /// Contains track metadata from MusicBrainz.
    /// </summary>
    public class TrackMetadata
    {
        public int Position { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public TimeSpan? Length { get; set; }
    }
}
