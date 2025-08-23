using QuranVideoGeneration.Web.Models;

namespace QuranVideoGeneration.Web.Services
{
    public interface IMediaService
    {
        Task<GenerateResult> GenerateVideoAsync(GenerateRequest req);
    }
}
