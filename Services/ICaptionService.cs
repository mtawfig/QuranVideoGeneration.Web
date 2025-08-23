using QuranVideoGeneration.Web.Models;

namespace QuranVideoGeneration.Web.Services
{
    public interface ICaptionService
    {
        /// <summary>
        /// Placeholder for proper Arabic overlay rendering via headless Chromium.
        /// Returns a list of caption PNG segments with timing.
        /// </summary>
        Task<List<CaptionSegment>> BuildCaptionsAsync(int surah, int fromAyah, int toAyah, string targetDir, TimeSpan totalAudio);
    }
}
