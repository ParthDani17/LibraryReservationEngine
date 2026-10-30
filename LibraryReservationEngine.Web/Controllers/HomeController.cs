using System.Diagnostics;
using LibraryReservationEngine.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LibraryReservationEngine.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Route("/Home/StatusCodeHandler")]
        public IActionResult StatusCodeHandler(int code)
        {
            _logger.LogWarning("HTTP status code {StatusCode} received for path {Path}", code, HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>()?.OriginalPath);

            if (code == 404)
            {
                Response.StatusCode = 404;
                return View("NotFound");
            }

            if (code == 403)
            {
                Response.StatusCode = 403;
                return View("AccessDenied");
            }

            Response.StatusCode = code;
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [Route("/Home/AccessDenied")]
        public IActionResult AccessDenied()
        {
            return View("AccessDenied");
        }

        [Route("/Home/NotFound")]
        public IActionResult NotFoundPage()
        {
            return View("NotFound");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            _logger.LogError("Unhandled error triggered with RequestId {RequestId}", requestId);
            return View(new ErrorViewModel { RequestId = requestId });
        }
    }
}
