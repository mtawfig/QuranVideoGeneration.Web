namespace QuranVideoGeneration.Web.Models
{
    public class Reciter
    {
        public string Code { get; set; } = "";
        public string DisplayName { get; set; } = "";
        /// <summary>
        /// Example: https://everyayah.com/data/Alafasy_128kbps
        /// </summary>
        public string BaseUrl { get; set; } = "";
        /// <summary>
        /// Example: {surah3}{ayah3}.mp3  -->  001001.mp3
        /// </summary>
        public string FilePattern { get; set; } = "{surah3}{ayah3}.mp3";
    }
}
