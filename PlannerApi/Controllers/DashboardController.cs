using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlannerApi.Repositories;

namespace PlannerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(IPostgresRepository repository) : ControllerBase
{
    private string? UserRole => User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))?.Value;
    private int? VendorId => int.TryParse(User.Claims.FirstOrDefault(c => c.Type == "vendor_id")?.Value, out var id) ? id : null;

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        if (UserRole == "VENDOR")
        {
            if (!VendorId.HasValue) return Unauthorized("VendorId missing in token. Please log out and log in again.");
            return Ok(await repository.GetVendorSummaryAsync(VendorId.Value));
        }

        return Ok(await repository.GetSummaryAsync());
    }

    [HttpGet("tasks/top")]
    public async Task<IActionResult> GetTopNewTasks()
    {
        if (UserRole == "VENDOR")
        {
            if (!VendorId.HasValue) return Unauthorized("VendorId missing in token. Please log out and log in again.");
            var vendorTasks = await repository.GetTasksForVendorAsync(VendorId.Value);
            return Ok(vendorTasks.Take(5));
        }

        var tasks = (await repository.GetTasksAsync())
            .Where(x => x.Status.Equals("New", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.ReceivedOn)
            .Take(5);

        return Ok(tasks);
    }
}
