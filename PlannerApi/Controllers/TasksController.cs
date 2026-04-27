
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlannerApi.DTOs;
using PlannerApi.Repositories;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace PlannerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConfiguration configuration;
    private readonly IPostgresRepository repository;
    public TasksController(
        IPostgresRepository repository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        this.repository = repository;
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
    }
    private string? UserRole => User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))?.Value;
    private int? VendorId => int.TryParse(User.Claims.FirstOrDefault(c => c.Type == "vendor_id")?.Value, out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await GetVisibleTasks());

    [HttpGet("vendor-queue")]
    public async Task<IActionResult> GetVendorQueue()
    {
        if (UserRole != "VENDOR") return Forbid();
        if (!VendorId.HasValue) return Unauthorized("VendorId missing in token. Please log out and log in again.");
        var items = await repository.GetTasksForVendorAsync(VendorId.Value);
        foreach (var task in items) task.Notes = string.Empty;
        return Ok(items);
    }

    [HttpGet("review-queue")]
    public async Task<IActionResult> GetReviewQueue() => Ok(await GetVisibleTasks(reviewOnly: true));

    [HttpGet("list")]
    public async Task<IActionResult> GetList(
    [FromQuery] string? status,
    [FromQuery] string? priority,
    [FromQuery] string? search,
    [FromQuery] string? role,
    [FromQuery] string? clientName,
    [FromQuery] bool? closingToday,
    [FromQuery] string? slaDate)
    {
        var result = await repository.GetPlannerListAsync(
            status, priority, search, role, clientName, closingToday, UserRole, VendorId, slaDate);

        return Ok(new
        {
            items = result.Items,
            totalCount = result.TotalCount
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var task = await repository.GetTaskAsync(id);
        if (task is null) return NotFound();
        if (IsVendorDenied(task)) return Forbid();
        if (UserRole == "VENDOR") task.Notes = string.Empty;
        return Ok(task);
    }

    [HttpGet("{id:int}/recommended-candidates")]
    public async Task<IActionResult> GetRecommendedCandidates(int id)
    {
        var task = await repository.GetTaskAsync(id);
        if (task is null) return NotFound();
        if (IsVendorDenied(task)) return Forbid();
        return Ok(await repository.GetRecommendedCandidatesAsync(id));
    }

    [HttpGet("{id:int}/recommended-vendors")]
    public async Task<IActionResult> GetRecommendedVendors(int id)
    {
        var task = await repository.GetTaskAsync(id);
        if (task is null) return NotFound();
        if (IsVendorDenied(task)) return Forbid();
        return Ok(await repository.GetRecommendedVendorsAsync(id));
    }

    [HttpGet("{id:int}/vendor-submissions")]
    public async Task<IActionResult> GetVendorSubmissions(int id)
    {
        var task = await repository.GetTaskAsync(id);
        if (task is null) return NotFound();
        if (IsVendorDenied(task)) return Forbid();
        var result = await repository.GetVendorSubmissionsAsync(id, UserRole == "VENDOR" ? VendorId : null);
        return Ok(new { vendorComment = result.VendorComment, assignmentNote = result.AssignmentNote, items = result.Items });
    }

    [HttpPost("{id:int}/vendor-submissions")]
    public async Task<IActionResult> SaveVendorSubmissions(int id, [FromBody] SaveVendorCandidatesRequest request)
    {
        if (UserRole != "VENDOR" || !VendorId.HasValue) return Forbid();
        var task = await repository.GetTaskAsync(id);
        if (task is null) return NotFound();
        if (IsVendorDenied(task)) return Forbid();
        await repository.SaveVendorSubmissionsAsync(id, VendorId.Value, request, User.Identity?.Name ?? "Vendor");
        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request)
    {
        if (UserRole == "VENDOR") return Forbid();
        var id = await repository.CreateTaskAsync(request.Subject, request.FromEmail, request.Body, "Manual Entry");
        var task = await repository.GetTaskAsync(id);
        return CreatedAtAction(nameof(GetById), new { id }, task);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTaskRequest request)
    {
        if (UserRole == "VENDOR") return Forbid();
        await repository.UpdateTaskAsync(id, request, User?.Identity?.Name ?? "Recruiter");
        return Ok(new { success = true, message = "Task saved successfully." });
    }

    [HttpPost("{id:int}/assign-vendors")]
    public async Task<IActionResult> AssignVendors(int id, [FromBody] AssignVendorsRequest request)
    {
        if (UserRole == "VENDOR") return Forbid();
        await repository.AssignVendorsAsync(id, request, User?.Identity?.Name ?? "Recruiter");
        return Ok(new { success = true, message = "Task assigned successfully." });
    }

    [HttpPost("upload-mail")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadMail(
       IFormFile? file,
       [FromForm] string? fromEmail,
       [FromForm] string? emailContent)
    {
        if (UserRole == "VENDOR")
            return Forbid();

        if ((file is null || file.Length == 0) && string.IsNullOrWhiteSpace(emailContent))
            return BadRequest("Mail file or email content is required.");

        if (file is not null && file.Length > 0)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var bytes = ms.ToArray();

            // 1. Send uploaded file to GPT file API
            ms.Position = 0;
            using var uploadStream = new MemoryStream(bytes);

            var uploadedFile = await UploadFileToGptApiAsync(
                file.FileName,
                uploadStream,
                file.ContentType);

            var gptFileId = uploadedFile.Id;

            if (string.IsNullOrWhiteSpace(gptFileId))
                throw new InvalidOperationException("GPT file upload succeeded but file id was not returned.");

            var extractorResult = await CallGptPromptAsync(
                configuration["GptFileApi:EmailExtractorUrl"]!,
                gptFileId,
                "Analyse this email attachment and extract JD information, client name, contact, budget, SLA and role details.");

            var jdAnalysisResult = await CallGptPromptAsync(
                configuration["GptFileApi:JdAnalyserUrl"]!,
                gptFileId,
                "Analyse this JD and identify key points, role requirements, risks and missing information.");

            var clarificationResult = await CallGptPromptAsync(
                configuration["GptFileApi:ClarificationUrl"]!,
                gptFileId,
                "Draft a formal client clarification email based on the missing JD details.");

            // 2. Existing planner task creation
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

    private async Task<GptFileUploadResponse> UploadFileToGptApiAsync(
    string fileName,
    Stream fileStream,
    string? contentType)
    {
        var apiUrl = configuration["GptFileApi:FileUploadUrl"];
        var base64Key = configuration["GptFileApi:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiUrl))
            throw new InvalidOperationException("GptFileApi:FileUploadUrl is missing.");

        if (string.IsNullOrWhiteSpace(base64Key))
            throw new InvalidOperationException("GptFileApi:ApiKey is missing.");

        var apiKey = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(base64Key));

        using var client = httpClientFactory.CreateClient("GptFileApi");

        using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.UserAgent.ParseAdd("PlannerApi/1.0");

        using var form = new MultipartFormDataContent();

        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType);

        form.Add(fileContent, "file", fileName);

        request.Content = form;

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"GPT file upload failed. Status: {(int)response.StatusCode}. Response: {body}");

        return System.Text.Json.JsonSerializer.Deserialize<GptFileUploadResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Unable to parse GPT file upload response.");
    }

    private async Task<GptPromptResponse> CallGptPromptAsync(
    string promptUrl,
    string fileId,
    string message)
    {
        var base64Key = configuration["GptFileApi:ApiKey"];
        var model = configuration["GptFileApi:Model"] ?? "tag-jd-cv-resume-analyser";

        if (string.IsNullOrWhiteSpace(base64Key))
            throw new InvalidOperationException("GptFileApi:ApiKey is missing.");

        var apiKey = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(base64Key));

        var payload = new
        {
            stream = false,
            model,
            messages = new[]
            {
            new
            {
                role = "user",
                content = message
            }
        },
            files = new[]
            {
            new
            {
                type = "file",
                id = fileId
            }
        }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);

        using var client = httpClientFactory.CreateClient("GptFileApi");

        using var request = new HttpRequestMessage(HttpMethod.Get, promptUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.UserAgent.ParseAdd("PlannerApi/1.0");

        request.Content = new StringContent(json);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"GPT prompt failed. Url: {promptUrl}. Status: {(int)response.StatusCode}. Response: {body}");

        return System.Text.Json.JsonSerializer.Deserialize<GptPromptResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new GptPromptResponse
            {
                Content = body
            };
    }

    private bool IsVendorDenied(PlannerApi.Models.PlannerTask task) => UserRole == "VENDOR" && (!VendorId.HasValue || !task.AssignedVendorIds.Contains(VendorId.Value));
    private async Task<IEnumerable<PlannerApi.Models.PlannerTask>> GetVisibleTasks(bool reviewOnly = false)
    {
        if (UserRole == "VENDOR")
        {
            if (!VendorId.HasValue) return [];
            var items = await repository.GetTasksForVendorAsync(VendorId.Value);
            foreach (var task in items) task.Notes = string.Empty;
            return items;
        }

        return reviewOnly ? await repository.GetReviewQueueAsync() : await repository.GetTasksAsync();
    }
}
