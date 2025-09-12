namespace QuranVideoGeneration.Web.Services
{
    public interface IPexelsService
    {
        Task<string?> DownloadRandomPexelsVideoAsync(string targetDir, string query);
    }
}
