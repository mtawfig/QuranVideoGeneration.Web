using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using QuranVideoGeneration.Web.Models;

namespace QuranVideoGeneration.Web.Services
{
    public class MediaService : IMediaService
    {
        private readonly IConfiguration _cfg;
        private readonly IPexelsService _pexels;
        private readonly IRecitationService _recitations;
        private readonly ICaptionService _captions;
        private readonly IWebHostEnvironment _env;

        private readonly string _ffmpeg;
        private readonly string _ffprobe;

        public MediaService(
            IConfiguration cfg,
            IPexelsService pexels,
            IRecitationService recitations,
            ICaptionService captions,
            IWebHostEnvironment env)
        {
            _cfg = cfg;
            _pexels = pexels;
            _recitations = recitations;
            _captions = captions;
            _env = env;

            // Resolve ffmpeg/ffprobe paths (use PATH if not configured)
            var bin = _cfg["FFmpeg:ExecutablePath"];
            string exe(string name) =>
                string.IsNullOrWhiteSpace(bin)
                    ? name
                    : (bin.EndsWith(name, StringComparison.OrdinalIgnoreCase) || bin.EndsWith($"{name}.exe", StringComparison.OrdinalIgnoreCase)
                        ? bin
                        : System.IO.Path.Combine(bin, OperatingSystem.IsWindows() ? $"{name}.exe" : name));

            _ffmpeg = exe("ffmpeg");
            _ffprobe = exe("ffprobe");
        }

        public async Task<GenerateResult> GenerateVideoAsync(GenerateRequest req)
        {
            var demoMode = _cfg.GetValue("Video:DemoMode", true);

            var outRoot = _cfg["Video:OutputRoot"];
            if (string.IsNullOrWhiteSpace(outRoot))
                outRoot = System.IO.Path.Combine(_env.WebRootPath, "generated");
            System.IO.Directory.CreateDirectory(outRoot);

            //var guid = Guid.NewGuid().ToString("N");
            var guid = DateTime.Now.Ticks.ToString();
            var workDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "qvg", guid);
            System.IO.Directory.CreateDirectory(workDir);

            var outFile = System.IO.Path.Combine(outRoot, $"qvg_{guid}.mp4");

            try
            {
                if (demoMode)
                {
                    // Simple 10s 1080x1920 black video with silent audio (no external deps)
                    await RunFfmpeg(
                        $"-y -f lavfi -i color=c=black:s=1080x1920:d=10 " +
                        $"-f lavfi -i anullsrc=channel_layout=stereo:sample_rate=44100 " +
                        $"-shortest -r 30 -c:v libx264 -pix_fmt yuv420p -c:a aac \"{outFile}\"");
                }
                else
                {
                    // Get QueryString from config
                    var url = _cfg["Pexels:BaseUrl"]??"https://api.pexels.com/videos/search?query=replace_with_query&orientation=portrait&resolution_name=1080p&people_count=0&min_duration=30&max_duration=60";
                    url = url.Replace("replace_with_query", req.VideoStyle??"islamic%20quran%20islam%20mosque", StringComparison.OrdinalIgnoreCase);
                    
                    // 1) Fetch background & recitation audio
                    var bgPath = await _pexels.DownloadRandomPexelsVideoAsync(workDir, url)
                                 ?? throw new InvalidOperationException("Failed to fetch background video from Pexels.");

                    var audioPath = await _recitations.DownloadAndConcatAsync(
                        req.ReciterCode, req.Surah, req.FromAyah, req.ToAyah, workDir);

                    if (!System.IO.File.Exists(audioPath))
                        throw new InvalidOperationException("Failed to fetch/concat recitation audio.");

                    // 2) Durations
                    var audioSeconds = await GetDurationSeconds(audioPath);
                    if (audioSeconds <= 0) throw new InvalidOperationException("Could not read audio duration.");

                    var bgSeconds = await GetVideoDurationSeconds(bgPath);
                    if (bgSeconds <= 0) bgSeconds = 5; // sane fallback

                    // 3) Build caption PNG overlays (even split until precise timings are added)
                    var overlays = await _captions.BuildCaptionsAsync(
                        req.Surah, req.FromAyah, req.ToAyah, workDir, TimeSpan.FromSeconds(audioSeconds));

                    // 4) Compose deterministically:
                    //    - Process bg once (scale->crop, 0.5× speed, eq, blur)
                    //    - Repeat via split+concat to cover audio (no -stream_loop)
                    //    - Add safe fades
                    //    - Overlay PNGs centered between(t,start,end)
                    var bgEffective = bgSeconds; // * 2.0; // 0.5× speed => duration doubles
                    var loops = (int)Math.Ceiling(audioSeconds / bgEffective);
                    loops = Math.Max(1, Math.Min(loops, 50)); // cap to keep graphs reasonable

                    // Inputs: 0=bg, 1=audio, 2..N=overlays
                    var inputs = new List<string>
                    {
                        $"-i \"{bgPath}\"",
                        $"-i \"{audioPath}\""
                    };
                    foreach (var ov in overlays)
                        inputs.Add($"-i \"{ov.PngPath}\"");

                    var sb = new StringBuilder();

                    // Step 1: base background with effects and 0.5× speed
                    sb.Append("[0:v]");
                    sb.Append("scale=-2:1920,crop=1080:1920,");
                    //sb.Append("setpts=2*PTS,"); // slow down to 0.5×
                    sb.Append("eq=contrast=1.06:brightness=0.03:saturation=1.1,");
                    sb.Append("boxblur=luma_radius=2:luma_power=1,");
                    sb.Append("format=rgba[bbase];");

                    // Step 2: split & concat 'loops' times to cover the whole audio
                    sb.Append($"[bbase]split={loops}");
                    for (int i = 0; i < loops; i++) sb.Append($"[s{i}]");
                    sb.Append(';');
                    for (int i = 0; i < loops; i++) sb.Append($"[s{i}]");
                    sb.Append($"concat=n={loops}:v=1:a=0[bgx];");

                    // Step 3: safe fade in/out based on audio duration
                    double fin = Math.Max(0.15, Math.Min(0.75, audioSeconds * 0.10)); // 10% bounded
                    double foutStart = Math.Max(0, audioSeconds - fin);
                    sb.Append($"[bgx]fade=t=in:st=0:d={fin.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"fade=t=out:st={foutStart.ToString(CultureInfo.InvariantCulture)}:d={fin.ToString(CultureInfo.InvariantCulture)}[bg0];");

                    // Step 4: overlays centered; nudge times to avoid zero-length windows
                    string last = "[bg0]";
                    for (int i = 0; i < overlays.Count; i++)
                    {
                        var start = overlays[i].Start.TotalSeconds + 0.001;
                        var end = overlays[i].End.TotalSeconds - 0.001;
                        if (end <= start) end = start + 0.05;

                        string ovIn = $"[{i + 2}:v]"; // inputs after bg (0) and audio (1)
                        string next = $"[bg{i + 1}]";
                        sb.Append($"{last}{ovIn}overlay=(W-w)/2:(H-h)/2:enable='between(t,{start.ToString(CultureInfo.InvariantCulture)},{end.ToString(CultureInfo.InvariantCulture)})'{next};");
                        last = next;
                    }

                    var filter = sb.ToString().TrimEnd(';');

                    var args = new StringBuilder();
                    args.Append("-y ");
                    args.Append(string.Join(" ", inputs)).Append(' ');
                    args.Append($"-filter_complex \"{filter}\" -map \"{last}\" -map 1:a ");
                    args.Append($"-t {audioSeconds.ToString(CultureInfo.InvariantCulture)} ");
                    args.Append("-c:v libx264 -pix_fmt yuv420p -c:a aac ");
                    args.Append($"\"{outFile}\"");

                    await RunFfmpeg(args.ToString());
                }

                var rel = $"/generated/{System.IO.Path.GetFileName(outFile)}";
                return new GenerateResult
                {
                    Success = true,
                    Message = "Video generated successfully",
                    PreviewUrl = rel,
                    DownloadUrl = rel,
                    FileName = System.IO.Path.GetFileName(outFile)
                };
            }
            catch (Exception ex)
            {
                return new GenerateResult { Success = false, Message = ex.Message };
            }
            finally
            {
                try { if (System.IO.Directory.Exists(workDir)) System.IO.Directory.Delete(workDir, true); } catch { }
            }
        }

        // --- Helpers ---------------------------------------------------------

        private async Task<double> GetDurationSeconds(string mediaPath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffprobe,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{mediaPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            string output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s);
            return s;
        }

        private async Task<double> GetVideoDurationSeconds(string mediaPath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffprobe,
                Arguments = $"-v error -select_streams v:0 -show_entries stream=duration -of default=nk=1:nw=1 \"{mediaPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            string output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();

            if (double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && s > 0)
                return s;

            // fallback: container duration
            return await GetDurationSeconds(mediaPath);
        }

        private async Task RunFfmpeg(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpeg,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi)!;
            string stderr = await p.StandardError.ReadToEndAsync();
            string stdout = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();

            if (p.ExitCode != 0)
            {
                var msg = string.Join(
                    Environment.NewLine,
                    $"FFmpeg failed with code {p.ExitCode}",
                    string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
                throw new InvalidOperationException(msg);
            }
        }
    }
}
