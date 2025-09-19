using Microsoft.AspNetCore.Mvc;

namespace QuranVideoGeneration.Web.Controllers
{
    public class GenerationsController : Controller
    {
        [HttpGet("/Generations")]
        public IActionResult Index() => View();
    }
}
