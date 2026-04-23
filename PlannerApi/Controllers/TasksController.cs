using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlannerApi.DTOs;
using PlannerApi.Repositories;

namespace PlannerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController(IPostgresRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await repository.GetTasksAsync());

    [HttpGet("review-queue")]
    public async Task<IActionResult> GetReviewQueue() => Ok(await repository.GetReviewQueueAsync());

    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] string? clientName,
        [FromQuery] bool? closingToday)
    {
        var userRole = User.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Role ||
                c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        var vendorIdClaim = User.Claims.FirstOrDefault(c => c.Type == "vendor_id")?.Value;
        int? vendorId = int.TryParse(vendorIdClaim, out var parsedVendorId) ? parsedVendorId : null;

        var result = await repository.GetPlannerListAsync(
            status,
            priority,
            search,
            role,
            clientName,
            closingToday,
            userRole,
            vendorId);

        return Ok(result);
    }

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

    [HttpGet("{id:int}/recommended-vendors")]
    public async Task<IActionResult> GetRecommendedVendors(int id)
    {
        var task = await repository.GetTaskAsync(id);
        if (task is null) return NotFound();

        return Ok(await repository.GetRecommendedVendorsAsync(id));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request)
    {
        var id = await repository.CreateTaskAsync(
            request.Subject,
            request.FromEmail,
            request.Body,
            "Manual Entry");

        var task = await repository.GetTaskAsync(id);
        return CreatedAtAction(nameof(GetById), new { id }, task);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTaskRequest request)
    {
        await repository.UpdateTaskAsync(id, request, User?.Identity?.Name ?? "Recruiter");
        return Ok(new { success = true, message = "Task saved successfully." });
    }

    [HttpPost("{id:int}/assign-vendors")]
    public async Task<IActionResult> AssignVendors(int id, [FromBody] AssignVendorsRequest request)
    {
        await repository.AssignVendorsAsync(id, request, User?.Identity?.Name ?? "Recruiter");
        return Ok(new { success = true, message = "Task assigned successfully." });
    }

    [HttpPost("upload-mail")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadMail(IFormFile? file, [FromForm] string? fromEmail, [FromForm] string? emailContent)
    {
        if ((file is null || file.Length == 0) && string.IsNullOrWhiteSpace(emailContent))
            return BadRequest("Mail file or email content is required.");

        if (file is not null && file.Length > 0)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            string content = file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToBase64String(bytes)
                : System.Text.Encoding.UTF8.GetString(bytes);

            var idFromFile = await repository.CreateTaskAsync(
                Path.GetFileNameWithoutExtension(file.FileName),
                fromEmail ?? "internal@dbiz.com",
                content,
                "Document Upload",
                file.FileName);

            var taskFromFile = await repository.GetTaskAsync(idFromFile);
            return CreatedAtAction(nameof(GetById), new { id = idFromFile }, taskFromFile);
        }

        var id = await repository.CreateTaskAsync(
            "JD Pasted Content",
            fromEmail ?? "internal@dbiz.com",
            emailContent ?? string.Empty,
            "Manual Paste");

        var task = await repository.GetTaskAsync(id);
        return CreatedAtAction(nameof(GetById), new { id }, task);
    }
}