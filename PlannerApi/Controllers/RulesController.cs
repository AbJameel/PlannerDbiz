using Microsoft.AspNetCore.Mvc;
using PlannerApi.Models;
using PlannerApi.Repositories;

namespace PlannerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RulesController(IPostgresRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await repository.GetRulesAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Rule rule)
    {
        var id = await repository.CreateRuleAsync(rule);
        return Ok(new { id });
    }
}
