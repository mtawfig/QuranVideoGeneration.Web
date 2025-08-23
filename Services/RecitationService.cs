using System.Text.Json;
using QuranVideoGeneration.Web.Models;
using System.Diagnostics;
using System.Linq;

namespace QuranVideoGeneration.Web.Services
{
    public class RecitationService : IRecitationService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _cfg;
        private readonly string _ffmpegExe;

        public RecitationService(IWebHostEnvironment env, IConfiguration cfg)
        {
            _env = env;
            _cfg = cfg;

            var configured = _cfg["FFmpeg:ExecutablePath"];
            _ffmpegExe = string.IsNullOrWhiteSpace(configured)
                ? "ffmpeg"
                : (configured.EndsWith("ffmpeg", StringComparison.OrdinalIgnoreCase) || configured.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)
                    ? configured
                    : Path.Combine(configured, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg"));
        }

        public async Task<string> DownloadAndConcatAsync(string reciterCode, int surah, int fromAyah, int toAyah, string targetDir)
        {
            var path = Path.Combine(_env.WebRootPath, "data", "reciters.json");
            var json = await File.ReadAllTextAsync(path);
            var reciters = JsonSerializer.Deserialize<List<Reciter>>(json) ?? new();

            var reciter = reciters.FirstOrDefault(r => r.Code.Equals(reciterCode, StringComparison.OrdinalIgnoreCase))
                          ?? throw new InvalidOperationException("Unknown reciter");

            Directory.CreateDirectory(targetDir);
            var http = new HttpClient();
            var parts = new List<string>();

            for (int a = fromAyah; a <= toAyah; a++)
            {
                var surah3 = surah.ToString("D3");
                var ayah3 = a.ToString("D3");
                var fileName = reciter.FilePattern.Replace("{surah3}", surah3).Replace("{ayah3}", ayah3);
                var url = $"{reciter.BaseUrl}/{fileName}";

                var dest = Path.Combine(targetDir, fileName);
                var bytes = await http.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(dest, bytes);
                parts.Add(dest);
            }

            // concat list
            var listFile = Path.Combine(targetDir, "concat.txt");
            var lines = parts.Select(p =>
            {
                var escaped = p.Replace("\\", "\\\\").Replace("'", "\\'");
                return $"file '{escaped}'";
            });
            await File.WriteAllLinesAsync(listFile, lines);

            var output = Path.Combine(targetDir, "recitation.mp3");
            await RunFfmpegAsync($"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{output}\"");
            return output;
        }

        private async Task RunFfmpegAsync(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegExe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = new Process { StartInfo = psi };
            proc.Start();
            _ = proc.StandardOutput.ReadToEndAsync();
            _ = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            if (proc.ExitCode != 0) throw new InvalidOperationException($"ffmpeg exited with code {proc.ExitCode}");
        }
    }
}
