using Microsoft.AspNetCore.Mvc;
using QuranVideoGeneration.Web.Models;
using QuranVideoGeneration.Web.Services;

namespace QuranVideoGeneration.Web.Controllers
{
    [Route("[controller]/[action]")]
    public class VideoController : Controller
    {
        private readonly IMediaService _media;

        public VideoController(IMediaService media)
        {
            _media = media;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate(GenerateRequest request)
        {
            if (!ModelState.IsValid || request.FromAyah <= 0 || request.ToAyah < request.FromAyah)
            {
                return BadRequest(new GenerateResult
                {
                    Success = false,
                    Message = "Invalid input. Check ayah range and required fields."
                });
            }

            var result = await _media.GenerateVideoAsync(request);
            if (!result.Success)
                return BadRequest(result);

            return Json(result);
        }
    }
}
