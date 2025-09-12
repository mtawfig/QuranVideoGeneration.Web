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

        public async Task<string?> DownloadRandomPexelsVideoAsync(string targetDir, string url)
        {
            var apiKey = _cfg["Pexels:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Pexels:ApiKey is not configured on the server.");

            if (!Uri.TryCreate(url, UriKind.Absolute, out var searchUri))
                throw new InvalidOperationException($"Pexels:QueryString is not a valid absolute URL: '{url}'.");

            var client = _httpFactory.CreateClient();

            using var req = new HttpRequestMessage(HttpMethod.Get, searchUri);
            // Pexels requires raw key, not Bearer
            req.Headers.TryAddWithoutValidation("Authorization", apiKey);

            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Pexels API returned {(int)resp.StatusCode} {resp.ReasonPhrase}. Check API key and URL.");

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var results = doc.RootElement.GetProperty("videos");
            if (results.GetArrayLength() == 0)
                throw new InvalidOperationException("Pexels returned no videos for your query.");

            var chosen = results[Random.Shared.Next(results.GetArrayLength())];
            var best = chosen.GetProperty("video_files")
                            .EnumerateArray()
                            .OrderByDescending(f => f.GetProperty("height").GetInt32() * f.GetProperty("width").GetInt32())
                            .First();

            var fileUrl = best.GetProperty("link").GetString();
            if (string.IsNullOrWhiteSpace(fileUrl))
                throw new InvalidOperationException("Pexels response did not contain a downloadable link.");

            Directory.CreateDirectory(targetDir);
            var dest = Path.Combine(targetDir, $"bg_{Guid.NewGuid():N}.mp4");

            using var fileResp = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
            fileResp.EnsureSuccessStatusCode();
            await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
            await fileResp.Content.CopyToAsync(fs);

            return dest;
        }

    }
}
