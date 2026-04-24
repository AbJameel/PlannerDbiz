
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
        var summary = await repository.GetSummaryAsync();
        if (UserRole == "VENDOR" && VendorId.HasValue)
        {
            var tasks = (await repository.GetTasksAsync()).Where(t => t.AssignedVendorIds.Contains(VendorId.Value)).ToList();
            summary.NewTasks = tasks.Count(t => t.Status.Equals("New", StringComparison.OrdinalIgnoreCase));
            summary.UnderReview = tasks.Count(t => t.Status.Equals("In Review", StringComparison.OrdinalIgnoreCase) || t.Status.Equals("Vendor Submitted", StringComparison.OrdinalIgnoreCase));
            summary.AssignedToVendors = tasks.Count(t => t.Status.Equals("Assigned to Vendor", StringComparison.OrdinalIgnoreCase));
            summary.ClosingToday = tasks.Count(t => t.SlaDate.Date == DateTime.Today);
        }
        return Ok(summary);
    }

    [HttpGet("tasks/top")]
    public async Task<IActionResult> GetTopNewTasks()
    {
        var tasks = (await repository.GetTasksAsync()).AsEnumerable();
        if (UserRole == "VENDOR" && VendorId.HasValue)
            tasks = tasks.Where(x => x.AssignedVendorIds.Contains(VendorId.Value));
        tasks = tasks.Where(x => x.Status.Equals("New", StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.ReceivedOn).Take(5);
        return Ok(tasks);
    }
}
