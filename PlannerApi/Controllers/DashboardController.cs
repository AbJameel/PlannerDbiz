using Microsoft.AspNetCore.Mvc;
using PlannerApi.Repositories;

namespace PlannerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController(IPostgresRepository repository) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary() => Ok(await repository.GetSummaryAsync());

    [HttpGet("tasks/top")]
    public async Task<IActionResult> GetTopNewTasks()
    {
        var tasks = (await repository.GetTasksAsync())
            .Where(x => x.Status.Equals("New", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.ReceivedOn)
            .Take(5);

        return Ok(tasks);
    }
}
