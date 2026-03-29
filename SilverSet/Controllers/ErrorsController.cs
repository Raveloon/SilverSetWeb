using Microsoft.AspNetCore.Mvc;

namespace SilverSet.Controllers;

public class ErrorsController : Controller
{
    [Route("Errors/{code:int}")]
    public IActionResult Index(int code)
    {
        return code switch
        {
            404 => View("NotFound"),
            500 => View("InternalError"),
            _ => View("GeneralError")
        };
    }
}