using Microsoft.AspNetCore.Mvc;
using PlannerApi.Models;
using PlannerApi.Repositories;

namespace PlannerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CandidatesController(IPostgresRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await repository.GetCandidatesAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Candidate candidate)
    {
        var id = await repository.CreateCandidateAsync(candidate);
        return Ok(new { id });
    }
}
