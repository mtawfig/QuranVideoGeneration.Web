using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.Json;
using QuranVideoGeneration.Web.Models;
using System.Runtime.Versioning;
using System.Xml.Linq;
using System.Text; // for UTF-8 StreamReader

namespace QuranVideoGeneration.Web.Services
{
    [SupportedOSPlatform("windows")] // quiet CA1416; project can also target net8.0-windows in csproj
    public class CaptionService : ICaptionService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _cfg;

        public CaptionService(IWebHostEnvironment env, IConfiguration cfg)
        {
            _env = env; _cfg = cfg;
        }

        public async Task<List<CaptionSegment>> BuildCaptionsAsync(
            int surah, int fromAyah, int toAyah, string targetDir, TimeSpan totalAudio)
        {
            var fontPath = _cfg["Video:Overlay:FontPath"];
            var fontSize = _cfg.GetValue("Video:Overlay:FontSize", 72);
            Directory.CreateDirectory(targetDir);

            // Pull ayah text for THIS surah from EveryAyah XML
            //var quran = await TryLoadQuranAsync(surah);
            var quran = await TryLoadQuranAsync();

            int count = Math.Max(1, toAyah - fromAyah + 1);
            var segMs = (int)(totalAudio.TotalMilliseconds / count);
            var list = new List<CaptionSegment>();

            for (int i = 0; i < count; i++)
            {
                int ay = fromAyah + i;
                string text = quran?.GetText(surah, ay) ?? $"آية {ay}";

                var png = Path.Combine(targetDir, $"ov_{i + 1:000}.png");
                RenderCenteredArabicPng(png, text, fontPath, fontSize);

                var start = TimeSpan.FromMilliseconds(segMs * i);
                var end = (i == count - 1) ? totalAudio : TimeSpan.FromMilliseconds(segMs * (i + 1));

                list.Add(new CaptionSegment { PngPath = png, Start = start, End = end });
            }
            return list;
        }

        // --- NEW: fetch from EveryAyah XML per surah ----------------------------------------

        // private async Task<QuranText?> TryLoadQuranAsync(int surah)
        // {
        //     // https://everyayah.com/data/XML/Arabic/Quran_Arabic_{surah:000}.xml
        //     var url = $"https://everyayah.com/data/XML/Arabic/Quran_Arabic_{surah:D3}.xml";
        //     try
        //     {
        //         using var http = new HttpClient();
        //         var xml = await http.GetStringAsync(url);
        //         var doc = XDocument.Parse(xml);

        //         // <sura id="..."><aya id="1" text="..."/>...</sura>
        //         var suraEl = doc.Root;
        //         if (suraEl is null || suraEl.Name.LocalName != "sura")
        //             return null;

        //         var items = suraEl.Elements("aya")
        //             .Select(e => new {
        //                 Id = int.TryParse((string?)e.Attribute("id"), out var n) ? n : -1,
        //                 Text = (string?)e.Attribute("text") ?? ""
        //             })
        //             .Where(a => a.Id >= 1) // skip id=0 (ta‘awwudh)
        //             .OrderBy(a => a.Id)
        //             .ToList();

        //         if (items.Count == 0) return null;

        //         var maxId = items.Max(a => a.Id);
        //         var list = Enumerable.Repeat(string.Empty, maxId).ToList();
        //         foreach (var a in items)
        //             list[a.Id - 1] = a.Text;

        //         var map = new Dictionary<int, List<string>> { [surah] = list };
        //         return new QuranText(map);
        //     }
        //     catch
        //     {
        //         return null;
        //     }
        // }

        // Cache to avoid re-parsing every request
        private static Dictionary<int, List<string>>? _quranCache;
        private static DateTime _quranMtimeUtc;
        private static readonly object _quranLock = new();

        private async Task<QuranText?> TryLoadQuranAsync()
        {
            // Local XML: wwwroot/data/quran_simple.xml
            var path = Path.Combine(_env.WebRootPath, "data", "quran_simple.xml");
            if (!File.Exists(path)) return null;

            var mtime = File.GetLastWriteTimeUtc(path);
            lock (_quranLock)
            {
                if (_quranCache != null && mtime == _quranMtimeUtc)
                    return new QuranText(_quranCache); // serve cached
            }

            // Read & parse
            string xml;
            using (var fs = File.OpenRead(path))
            using (var sr = new StreamReader(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true))
                xml = await sr.ReadToEndAsync();

            var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var root = doc.Root;
            if (root is null || root.Name.LocalName != "quran")
                return null;

            var map = new Dictionary<int, List<string>>(128);

            foreach (var suraEl in root.Elements("sura"))
            {
                var idxAttr = suraEl.Attribute("index")?.Value;
                if (!int.TryParse(idxAttr, out var surah)) continue;

                // Collect ayahs ordered by index (1-based in your file)
                var ayahs = suraEl.Elements("aya")
                    .Select(a => new
                    {
                        Index = int.TryParse(a.Attribute("index")?.Value, out var n) ? n : -1,
                        Text  = a.Attribute("text")?.Value ?? string.Empty
                    })
                    .Where(x => x.Index >= 1)
                    .OrderBy(x => x.Index)
                    .Select(x => x.Text)
                    .ToList();

                if (ayahs.Count > 0)
                    map[surah] = ayahs;
            }

            lock (_quranLock)
            {
                _quranCache = map;
                _quranMtimeUtc = mtime;
            }
            return new QuranText(map);
        }

        // --- Rendering ----------------------------------------------------------------------

        private void RenderCenteredArabicPng(string path, string text, string? fontPath, int fontSize)
        {
            const int W = 1080, H = 1920;
            using var bmp = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            // Load from explicit font file first
            using var pfc = new PrivateFontCollection();
            FontFamily? fam = null;

            if (!string.IsNullOrWhiteSpace(fontPath) && File.Exists(fontPath))
            {
                try { pfc.AddFontFile(fontPath); fam = pfc.Families.FirstOrDefault(); } catch { }
            }

            // Fallback to installed families by name
            if (fam == null)
            {
                var candidates = new[]
                {
                    "Amiri Quran", "Amiri",
                    "Scheherazade New", "Scheherazade",
                    "Noto Naskh Arabic",
                    "Segoe UI"
                };
                foreach (var name in candidates)
                {
                    try { fam = new FontFamily(name); break; } catch { }
                }
            }

            using var font = new Font(fam ?? FontFamily.GenericSansSerif, fontSize, FontStyle.Regular, GraphicsUnit.Pixel);

            using var format = new StringFormat(StringFormatFlags.LineLimit)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                HotkeyPrefix = HotkeyPrefix.None
            };
            // RTL for Arabic
            format.FormatFlags |= StringFormatFlags.DirectionRightToLeft;

            var rect = new RectangleF(80, 480, W - 160, H - 960);

            // Backdrop FIRST
            using (var bgBrush = new SolidBrush(Color.FromArgb(64, 0, 0, 0)))
            {
                var pad = 24;
                g.FillRectangle(bgBrush, new RectangleF(rect.X - pad, rect.Y - pad, rect.Width + pad * 2, rect.Height + pad * 2));
            }

            // Soft shadow
            using var shadowBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
            foreach (var (dx, dy) in new[] { (-3, -3), (3, -3), (-3, 3), (3, 3) })
                g.DrawString(text, font, shadowBrush, new RectangleF(rect.X + dx, rect.Y + dy, rect.Width, rect.Height), format);

            // Main text
            using var textBrush = new SolidBrush(Color.White);
            g.DrawString(text, font, textBrush, rect, format);

            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }

        // --- Minimal model for lookups ------------------------------------------------------

        private sealed class QuranText
        {
            private readonly Dictionary<int, List<string>> _map;
            public QuranText(Dictionary<int, List<string>> map) => _map = map;

            public string? GetText(int surah, int ayah)
                    => _map.TryGetValue(surah, out var list) &&
                    ayah >= 1 && ayah <= list.Count &&
                    !string.IsNullOrEmpty(list[ayah - 1])
                    ? list[ayah - 1]
                    : null;
        }

    }
}
