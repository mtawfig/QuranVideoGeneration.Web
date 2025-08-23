namespace QuranVideoGeneration.Web.Services
{
    public interface IPexelsService
    {
        Task<string?> DownloadRandomPortraitNatureAsync(string targetDir);
    }
}
