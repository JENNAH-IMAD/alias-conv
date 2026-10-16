using Microsoft.AspNetCore.Mvc;
namespace BankStatementConverter.API;

[ApiController, Route("api/settings/ocr")]
public sealed class OcrSettingsController(OcrSettingsStore settings) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(settings.PublicSettings);
    }
    [HttpPut]
    public IActionResult Save(OcrSettingsInput input)
    {
        settings.Save(input);
        Response.Headers.CacheControl = "no-store";
        return Ok(settings.PublicSettings);
    }
}
