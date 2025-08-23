namespace QuranVideoGeneration.Web.Services
{
    public interface IRecitationService
    {
        /// <summary>
        /// Downloads ayah mp3 files for the range and concatenates them into a single mp3.
        /// Returns the mp3 file path.
        /// </summary>
        Task<string> DownloadAndConcatAsync(string reciterCode, int surah, int fromAyah, int toAyah, string targetDir);
    }
}
