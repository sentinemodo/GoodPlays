using Microsoft.AspNetCore.Mvc;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/imports")]
public class ImportsController : ControllerBase
{
    [HttpPost]
    public IActionResult CreateImport()
    {
        // TODO(architecture): modules-and-integrations — ImportParseText Hangfire job (Phase 1)
        return StatusCode(StatusCodes.Status501NotImplemented, new { message = "Import pipeline not implemented (Phase 0 stub)." });
    }
}
