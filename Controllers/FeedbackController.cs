using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace QuranVideoGeneration.Web.Controllers
{
    public class FeedbackController : Controller
    {
        private static readonly SemaphoreSlim IoLock = new(1, 1);
        private readonly IWebHostEnvironment _env;

        //public FeedbackController(IWebHostEnvironment env) => _env = env;

        private readonly IConfiguration _cfg;
        private readonly IHttpClientFactory _http;

        public FeedbackController(IWebHostEnvironment env, IConfiguration cfg, IHttpClientFactory http)
        {
            _env = env;
            _cfg = cfg;
            _http = http;
        }

        // ✅ POCO model (works with DataAnnotations without the record quirk)
        public class FeedbackInput
        {
            [Required, MaxLength(200)]
            public string FullName { get; set; } = string.Empty;

            [EmailAddress, MaxLength(200)]
            public string? Email { get; set; }

            [Required, MaxLength(200)]
            public string Subject { get; set; } = string.Empty;

            [Required, MaxLength(4000)]
            public string Message { get; set; } = string.Empty;
        }

        public class FeedbackRecord
        {
            public string fullname { get; set; } = default!;
            public string? email { get; set; }
            public string subject { get; set; } = default!;
            public string message { get; set; } = default!;
            public string createdatetime { get; set; } = default!;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send([FromForm] FeedbackInput input)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Please fill all required fields correctly." });

            // --- Turnstile server-side verification ---
            var token = Request.Form["cf-turnstile-response"].ToString();
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { success = false, message = "Captcha is missing." });

            var verified = await VerifyTurnstileAsync(token);
            if (!verified)
                return BadRequest(new { success = false, message = "Captcha failed. Please try again." });
            // ------------------------------------------

            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Please fill all required fields correctly." });

            var feedbackDir = Path.Combine(_env.WebRootPath, "feedback");
            Directory.CreateDirectory(feedbackDir);

            var fileName = $"{DateTime.Now:dd-MM-yyyy}_feedback.json";
            var path = Path.Combine(feedbackDir, fileName);

            var record = new FeedbackRecord
            {
                fullname = input.FullName.Trim(),
                email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email!.Trim(),
                subject = input.Subject.Trim(),
                message = input.Message.Trim(),
                createdatetime = DateTime.Now.ToString("dd-MM-yyyy HH:mm")
            };

            var opts = new JsonSerializerOptions { WriteIndented = true };

            await IoLock.WaitAsync();
            try
            {
                List<FeedbackRecord> list;
                if (System.IO.File.Exists(path))
                {
                    await using var rs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    list = await JsonSerializer.DeserializeAsync<List<FeedbackRecord>>(rs) ?? new List<FeedbackRecord>();
                }
                else
                {
                    list = new List<FeedbackRecord>();
                }

                list.Add(record);

                await using var ws = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(ws, list, opts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Failed to save feedback: {ex.Message}" });
            }
            finally
            {
                IoLock.Release();
            }

            return Json(new { success = true });
        }

        private async Task<bool> VerifyTurnstileAsync(string token)
        {
            var secret = _cfg["Turnstile:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                // Fail closed if no secret configured
                return false;
            }

            using var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("secret",   secret),
                new KeyValuePair<string,string>("response", token),
                new KeyValuePair<string,string>("remoteip", HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty)
            });

            var client = _http.CreateClient();
            using var resp = await client.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", form);
            if (!resp.IsSuccessStatusCode) return false;

            var result = await resp.Content.ReadFromJsonAsync<TurnstileVerifyResponse>();
            // Optional: inspect result?.error_codes for logging
            return result?.success == true;
        }

        private sealed class TurnstileVerifyResponse
        {
            public bool success { get; set; }
            public string? challenge_ts { get; set; }
            public string? hostname { get; set; }
            public string[]? error_codes { get; set; }
            public string? action { get; set; }
            public string? cdata { get; set; }
        }
    }
}
