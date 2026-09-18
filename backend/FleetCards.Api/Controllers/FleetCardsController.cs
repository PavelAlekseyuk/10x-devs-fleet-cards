using Microsoft.AspNetCore.Mvc;

namespace FleetCards.Api.Controllers;

[ApiController]
[Route("api/fleet-cards")]
public sealed class FleetCardsController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<FleetCard>> Get()
    {
        return Ok(new[]
        {
            new FleetCard("Aurora", "Scout vessel", "Ready"),
            new FleetCard("Waypoint", "Command frigate", "On mission"),
            new FleetCard("Northstar", "Support cruiser", "Maintenance"),
        });
    }

    public sealed record FleetCard(string Name, string Role, string Status);
}
