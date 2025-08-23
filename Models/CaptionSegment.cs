namespace QuranVideoGeneration.Web.Models
{
    public class CaptionSegment
    {
        public string PngPath { get; set; } = "";
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
    }
}
