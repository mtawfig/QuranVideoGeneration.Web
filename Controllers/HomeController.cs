using Microsoft.AspNetCore.Mvc;

namespace QuranVideoGeneration.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
