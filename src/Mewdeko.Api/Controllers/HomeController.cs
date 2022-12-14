using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.WebApp.Controllers;

public class HomeController : Controller
{
    [HttpGet("~/")]
    public ActionResult Index() => View();
}