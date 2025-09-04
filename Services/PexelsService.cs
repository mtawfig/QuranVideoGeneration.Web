using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text.Json;

namespace QuranVideoGeneration.Web.Services
{
    public class PexelsService : IPexelsService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _cfg;

        public PexelsService(IHttpClientFactory httpFactory, IConfiguration cfg)
        {
            _httpFactory = httpFactory;
            _cfg = cfg;
        }

        public async Task<string?> DownloadRandomPortraitNatureAsync(string targetDir)
        {
            var apiKey = _cfg["Pexels:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return null;

            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(apiKey);

            //var url = "https://api.pexels.com/videos/search?query=nature&orientation=portrait&per_page=30&size=large";
            //var url = "https://api.pexels.com/videos/search?query=islamic&orientation=portrait&per_page=30&size=large";
            var url = "https://api.pexels.com/videos/search?query=islamic%20nature%20sea%20forest%20ocean&orientation=portrait&resolution_name=1080p&people_count=0&min_duration=30&max_duration=60";
            using var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var results = doc.RootElement.GetProperty("videos");
            if (results.GetArrayLength() == 0) return null;

            var rand = new Random();
            var idx = rand.Next(results.GetArrayLength());
            var chosen = results[idx];

            // Pick the highest quality file
            var files = chosen.GetProperty("video_files");
            var best = files.EnumerateArray()
                            .OrderByDescending(f => f.GetProperty("height").GetInt32() * f.GetProperty("width").GetInt32())
                            .First();
            var fileUrl = best.GetProperty("link").GetString();
            if (string.IsNullOrWhiteSpace(fileUrl)) return null;

            Directory.CreateDirectory(targetDir);
            var dest = Path.Combine(targetDir, "bg.mp4");
            using var fileResp = await client.GetAsync(fileUrl);
            fileResp.EnsureSuccessStatusCode();
            await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
            await fileResp.Content.CopyToAsync(fs);
            return dest;
        }
    }
}
