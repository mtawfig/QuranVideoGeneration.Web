namespace QuranVideoGeneration.Web.Models
{
    public class GenerateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? PreviewUrl { get; set; }
        public string? DownloadUrl { get; set; }
        public string? FileName { get; set; }
    }
}
