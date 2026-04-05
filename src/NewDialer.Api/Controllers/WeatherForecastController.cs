using Microsoft.AspNetCore.Mvc;

namespace NewDialer.Api.Controllers;

[ApiController]
[Route("weatherforecast")]
public sealed class WeatherForecastController : ControllerBase
{
    [HttpGet]
    public ActionResult<object> Get()
    {
        return Ok(new { Message = "NewDialer API is running." });
    }
}
