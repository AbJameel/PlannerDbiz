using Microsoft.AspNetCore.Mvc;
using PlannerApi.DTOs;
using PlannerApi.Repositories;

namespace PlannerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController(IPostgresRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await repository.GetTasksAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var task = await repository.GetTaskAsync(id);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpGet("{id:int}/recommended-candidates")]
    public async Task<IActionResult> GetRecommendedCandidates(int id)
    {
        var task = await repository.GetTaskAsync(id);
        if (task is null) return NotFound();

        return Ok(await repository.GetRecommendedCandidatesAsync(id));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request)
    {
        var id = await repository.CreateTaskAsync(request.Subject, request.FromEmail, request.Body, "Manual Entry");
        var task = await repository.GetTaskAsync(id);
        return CreatedAtAction(nameof(GetById), new { id }, task);
    }

    [HttpPost("upload-mail")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadMail(IFormFile? file, [FromForm] string? fromEmail)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Mail file is required.");
        }

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        var subject = file.FileName;
        var id = await repository.CreateTaskAsync(subject, fromEmail ?? "uploaded@mail.local", content, "Uploaded Mail");
        var task = await repository.GetTaskAsync(id);
        return CreatedAtAction(nameof(GetById), new { id }, task);
    }
}
