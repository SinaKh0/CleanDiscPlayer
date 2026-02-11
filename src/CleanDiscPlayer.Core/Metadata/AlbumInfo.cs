namespace CleanDiscPlayer.Core.Metadata
{
    /// <summary>
    /// Contains album metadata retrieved from MusicBrainz.
    /// </summary>
    public class AlbumInfo
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string ReleaseDate { get; set; }
        public string MusicBrainzId { get; set; }
        public List<TrackMetadata> Tracks { get; set; } = new();
    }
}
