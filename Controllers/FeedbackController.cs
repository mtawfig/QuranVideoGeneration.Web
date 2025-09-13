using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace QuranVideoGeneration.Web.Controllers
{
    public class FeedbackController : Controller
    {
        private static readonly SemaphoreSlim IoLock = new(1, 1);
        private readonly IWebHostEnvironment _env;

        public FeedbackController(IWebHostEnvironment env) => _env = env;

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
    }
}
