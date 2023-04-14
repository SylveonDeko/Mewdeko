using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Api.Controllers;

public class HomeController : Controller
{
    [HttpGet("~/")]
    public ActionResult Index() => View();
}