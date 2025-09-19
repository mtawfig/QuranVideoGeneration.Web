using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

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
