
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlannerApi.DTOs;
using PlannerApi.Repositories;
using PlannerApi.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        return Ok(new { vendorComment = result.VendorComment, items = result.Items });
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

        if (file is null || file.Length == 0)
        {
            var manualId = await repository.CreateTaskAsync(
                "JD Pasted Content",
                fromEmail ?? "internal@dbiz.com",
                emailContent ?? string.Empty,
                "Manual Paste");

            var manualTask = await repository.GetTaskAsync(manualId);
            return CreatedAtAction(nameof(GetById), new { id = manualId }, manualTask);
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var uploadedFile = await UploadFileToGptApiAsync(
            file.FileName,
            new MemoryStream(bytes),
            file.ContentType);

        var gptFileId = uploadedFile.Id;
        if (string.IsNullOrWhiteSpace(gptFileId))
            throw new InvalidOperationException("GPT file upload succeeded but file id was not returned.");

        var extractorResult = await CallGptPromptAsync(
            configuration["GptFileApi:EmailExtractorUrl"]!,
            gptFileId,
            BuildExtractorMessage(emailContent));

        var jdAnalysisResult = await CallGptPromptAsync(
            configuration["GptFileApi:JdAnalyserUrl"]!,
            gptFileId,
            BuildAnalysisMessage(emailContent));

        var extractedJobs = ParseExtractedJobs(extractorResult.Content);
        if (extractedJobs.Count == 0)
        {
            extractedJobs.Add(new GptExtractedJobInfo
            {
                Sender = fromEmail ?? "internal@dbiz.com",
                Title = Path.GetFileNameWithoutExtension(file.FileName),
                Role = Path.GetFileNameWithoutExtension(file.FileName),
                JobDetails = emailContent ?? string.Empty
            });
        }

        var gaps = ExtractGapsForClarification(jdAnalysisResult.Content);
        var firstRole = extractedJobs.FirstOrDefault()?.Role ?? extractedJobs.FirstOrDefault()?.Title ?? Path.GetFileNameWithoutExtension(file.FileName);

        var clarificationResult = await CallGptPromptAsync(
            configuration["GptFileApi:ClarificationUrl"]!,
            gptFileId,
            BuildClarificationMessage(jdAnalysisResult.Content, gaps, firstRole));

        var clarificationEmail = BuildClarificationEmail(
            clarificationResult.Content,
            jdAnalysisResult.Content,
            gaps,
            firstRole);

        var taskIds = new List<int>();
        foreach (var job in extractedJobs)
        {
            var taskId = await repository.CreateTaskFromGptExtractionAsync(
                job,
                Path.GetFileNameWithoutExtension(file.FileName),
                fromEmail ?? "internal@dbiz.com",
                "Document Upload - GPT Analysis",
                file.FileName,
                gptFileId,
                extractorResult.Content ?? "[]",
                jdAnalysisResult.Content ?? string.Empty,
                clarificationEmail);

            await repository.SaveGapAnalysisReplyAsync(
                taskId,
                gptFileId,
                jdAnalysisResult.Content ?? string.Empty,
                clarificationEmail,
                NormalizeExtractorJson(extractorResult.Content));

            taskIds.Add(taskId);
        }

        if (taskIds.Count == 1)
        {
            var task = await repository.GetTaskAsync(taskIds[0]);
            return CreatedAtAction(nameof(GetById), new { id = taskIds[0] }, task);
        }

        return Ok(new
        {
            success = true,
            gptFileId,
            createdCount = taskIds.Count,
            taskIds
        });
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

    private static string BuildExtractorMessage(string? emailContent) => $@"
Analyze the email body and uploaded attachment. Extract all job requirements, client/requestor details, budget if present, deadline/SLA, role, headcount and contact details.
Return strict JSON array only as per the configured extractor prompt.

Email Body:
{emailContent ?? string.Empty}";

    private static string BuildAnalysisMessage(string? emailContent) => $@"
Analyze the JD from the uploaded attachment and email body. Return critical requirements and gaps/missing information that require customer clarification.

Email Body:
{emailContent ?? string.Empty}";

    private static string BuildClarificationMessage(string? jdAnalysis, IReadOnlyList<string> gaps, string role) => $@"
Create a formal, short and crisp clarification email to the client for the {role} JD.
Use only the below JD analysis gaps/missing information as clarification points.

Gaps:
{string.Join("\n", gaps.Select(x => "- " + x))}

JD Analysis:
{jdAnalysis ?? string.Empty}";

    private static List<GptExtractedJobInfo> ParseExtractedJobs(string? content)
    {
        var json = NormalizeExtractorJson(content);
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<GptExtractedJobInfo>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizeExtractorJson(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "[]";

        var trimmed = content.Trim();
        var jsonStart = trimmed.IndexOf('[');
        var jsonEnd = trimmed.LastIndexOf(']');

        if (jsonStart < 0 || jsonEnd <= jsonStart)
            return "[]";

        var json = trimmed.Substring(jsonStart, jsonEnd - jsonStart + 1);

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return "[]";

            return JsonSerializer.Serialize(doc.RootElement);
        }
        catch
        {
            return "[]";
        }
    }

    private static List<string> ExtractGapsForClarification(string? analysis)
    {
        var gaps = new List<string>();
        if (string.IsNullOrWhiteSpace(analysis))
            return gaps;

        var capture = false;
        foreach (var rawLine in analysis.Replace("\r", string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (line.Contains("Gaps", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Missing", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Clarification", StringComparison.OrdinalIgnoreCase))
            {
                capture = true;
                continue;
            }

            if (capture && Regex.IsMatch(line, @"^\d+\s*[).]|^[-*•]"))
            {
                var cleaned = Regex.Replace(line, @"^(\d+\s*[).]|[-*•])\s*", "").Trim();
                if (!string.IsNullOrWhiteSpace(cleaned))
                    gaps.Add(cleaned);
            }
        }

        return gaps.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
    }

    private static string BuildClarificationEmail(string? apiContent, string? jdAnalysis, IReadOnlyList<string> gaps, string role)
    {
        var content = apiContent?.Trim() ?? string.Empty;

        if (content.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase) &&
            !content.Contains("Task:", StringComparison.OrdinalIgnoreCase) &&
            !content.Contains("Output Format:", StringComparison.OrdinalIgnoreCase))
        {
            return content;
        }

        var finalGaps = gaps.Count > 0 ? gaps : ExtractGapsForClarification(jdAnalysis);
        if (finalGaps.Count == 0)
            finalGaps = ["Please confirm any missing or unclear information in the shared JD."];

        return $@"Subject: Clarification Required on {role} JD

Dear Client Team,

Thank you for sharing the detailed Job Description for the {role} position.

To ensure we source the most suitable candidates and streamline the recruitment process, we seek your clarification on a few points:

{string.Join("\n", finalGaps.Select(x => "- " + x))}

We appreciate your guidance on the above to proceed effectively.

Thank you,
DBiz.ai Talent Acquisition Group";
    }

    private bool IsVendorDenied(PlannerApi.Models.PlannerTask task) => UserRole == "VENDOR" && (!VendorId.HasValue || !task.AssignedVendorIds.Contains(VendorId.Value));
    private async Task<IEnumerable<PlannerApi.Models.PlannerTask>> GetVisibleTasks(bool reviewOnly = false)
    {
        var tasks = reviewOnly ? await repository.GetReviewQueueAsync() : await repository.GetTasksAsync();
        if (UserRole == "VENDOR" && VendorId.HasValue) return tasks.Where(t => t.AssignedVendorIds.Contains(VendorId.Value));
        return tasks;
    }
}
