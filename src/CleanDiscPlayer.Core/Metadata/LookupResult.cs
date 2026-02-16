namespace CleanDiscPlayer.Core.Metadata
{
    /// <summary>
    /// The result of a metadata lookup operation, containing either the retrieved album information or details about the failure.
    /// </summary>
    public class LookupResult
    {
        public bool Success { get; set; }
        public AlbumInfo? Album { get; set; }
        public string? ErrorMessage { get; set; }
        public LookupErrorType ErrorType { get; set; }

        public static LookupResult Ok(AlbumInfo album) => new()
        {
            Success = true,
            Album = album
        };

        public static LookupResult Fail(string message, LookupErrorType type) => new()
        {
            Success = false,
            ErrorMessage = message,
            ErrorType = type
        };
    }

    public enum LookupErrorType
    {
        None,
        NotFound,
        NetworkError,
        SslError,
        RateLimited,
        Timeout,
        Unknown
    }
}
