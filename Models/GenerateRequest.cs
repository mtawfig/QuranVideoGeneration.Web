using System.ComponentModel.DataAnnotations;

namespace QuranVideoGeneration.Web.Models
{
    public class GenerateRequest
    {
        [Required]
        public string ReciterCode { get; set; } = "MISHARY";

        [Range(1, 114)]
        public int Surah { get; set; } = 1;

        [Range(1, 286)]
        public int FromAyah { get; set; } = 1;

        [Range(1, 286)]
        public int ToAyah { get; set; } = 7;

        [Required]
        public string VideoStyle { get; set; } = "nature";
    }
}
