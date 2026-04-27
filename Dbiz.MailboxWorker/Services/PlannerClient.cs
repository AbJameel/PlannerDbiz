using System.Text.Json;
using Dbiz.MailboxWorker.Models;
using Dbiz.MailboxWorker.Options;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Dbiz.MailboxWorker.Services;

public sealed class PlannerClient : IPlannerClient
{
    private readonly ILogger<PlannerClient> _logger;
    private readonly DatabaseOptions _options;

    public PlannerClient(ILogger<PlannerClient> logger, IOptions<DatabaseOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _options.Validate();
    }

    public async Task<bool> CreatePlannerAsync(CreatePlannerFromMailRequest request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Database loading is disabled. Parsed message {MessageId} not inserted.", request.MessageId);
            return true;
        }

        try
        {
            await using var connection = new NpgsqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var tx = await connection.BeginTransactionAsync(cancellationToken);

            var plannerNo = await GeneratePlannerNoAsync(connection, tx, cancellationToken);
            var extraction = request.Extraction ?? new PlannerMailExtraction();
            var receivedOn = request.ReceivedDateTime?.UtcDateTime ?? DateTime.UtcNow;
            var slaDate = extraction.SubmissionDeadline?.UtcDateTime
                          ?? receivedOn.AddDays(3);
            var budget = extraction.BudgetAmount ?? 0m;
            var budgetMax = extraction.BudgetMaxAmount;
            var clientName = FirstNotBlank(extraction.ClientName, "DBiz Internal");
            var role = FirstNotBlank(extraction.Role, request.Subject, "General Requirement");
            var contactName = FirstNotBlank(extraction.ClientContactName, request.FromName, "Internal Request");
            var contactEmail = FirstNotBlank(request.FromAddress, extraction.VendorContactEmail, "internal@dbiz.com");
            var requirementTitle = FirstNotBlank(extraction.RequirementTitle, role);
            var requirementAsked = FirstNotBlank(extraction.RequirementAsked, request.JobDescriptionText, request.BodyText);
            var timelineJson = JsonSerializer.Serialize(extraction.Timeline);
            var skillsJson = JsonSerializer.Serialize(extraction.PrimarySkills);
            var gapsJson = JsonSerializer.Serialize(extraction.Gaps);
            var secondarySkillsJson = JsonSerializer.Serialize(extraction.SecondarySkills);
            var emptyIdsJson = "[]";
            var notes = FirstNotBlank(extraction.Notes, string.Empty);
            var priority = FirstNotBlank(extraction.Priority, "Medium");
            var category = FirstNotBlank(extraction.Category, "General");
            var location = extraction.Location ?? string.Empty;
            var workMode = extraction.WorkMode ?? string.Empty;
            var employmentType = extraction.EmploymentType ?? string.Empty;
            var experienceRequired = extraction.ExperienceRequired ?? string.Empty;
            var sourceType = FirstNotBlank(extraction.SourceType, "Mailbox");
            var status = "New";

            var plannerId = await InsertPlannerAsync(connection, tx, plannerNo, request, extraction, cancellationToken);
            await InsertPlannerTaskAsync(connection, tx, plannerId, plannerNo, clientName, role, priority, budget, budgetMax,
                receivedOn, slaDate, status, extraction.NumberOfPersonnel ?? 1, sourceType, contactName, contactEmail,
                requirementAsked, skillsJson, gapsJson, timelineJson, requirementTitle, category, notes,
                secondarySkillsJson, experienceRequired, location, workMode, employmentType, cancellationToken);
            await InsertPlannerEmailAsync(connection, tx, plannerId, request, cancellationToken);
            await InsertPlannerAttachmentsAsync(connection, tx, plannerId, request.Attachments, cancellationToken);
            await InsertPlannerActivitiesAsync(connection, tx, plannerId, request, extraction, cancellationToken);

            await tx.CommitAsync(cancellationToken);
            _logger.LogInformation("Inserted planner task {PlannerNo} for message {MessageId}.", plannerNo, request.MessageId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert planner task for message {MessageId}.", request.MessageId);
            return false;
        }
    }

    private static async Task<string> GeneratePlannerNoAsync(NpgsqlConnection connection, NpgsqlTransaction tx, CancellationToken cancellationToken)
    {
        const string sql = "select 'PLN-' || to_char(now(), 'YYYYMMDDHH24MISS') || lpad(floor(random()*1000)::text, 3, '0');";
        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        var result = (string?)await cmd.ExecuteScalarAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(result) ? $"PLN-{DateTime.UtcNow:yyyyMMddHHmmssfff}" : result;
    }

    private static async Task<int> InsertPlannerAsync(NpgsqlConnection connection, NpgsqlTransaction tx, string plannerNo,
        CreatePlannerFromMailRequest request, PlannerMailExtraction extraction, CancellationToken cancellationToken)
    {
        const string sql = @"
insert into public.planner
(planner_no, client_name, requirement_title, role_name, budget_min, budget_max, currency, sla_date,
 contact_name, contact_email, requirement_summary, requirement_asked, gaps, status, source_email_id,
 conversation_id, updated_on)
values
(@planner_no, @client_name, @requirement_title, @role_name, @budget_min, @budget_max, @currency, @sla_date,
 @contact_name, @contact_email, @requirement_summary, @requirement_asked, @gaps, @status, @source_email_id,
 @conversation_id, now())
returning planner_id;";

        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("planner_no", plannerNo);
        cmd.Parameters.AddWithValue("client_name", (object?)extraction.ClientName ?? "DBiz Internal");
        cmd.Parameters.AddWithValue("requirement_title", (object?)extraction.RequirementTitle ?? extraction.Role ?? request.Subject);
        cmd.Parameters.AddWithValue("role_name", (object?)extraction.Role ?? request.Subject);
        cmd.Parameters.AddWithValue("budget_min", NpgsqlDbType.Numeric, (object?)extraction.BudgetAmount ?? 0m);
        cmd.Parameters.AddWithValue("budget_max", NpgsqlDbType.Numeric, (object?)extraction.BudgetMaxAmount ?? DBNull.Value);
        cmd.Parameters.AddWithValue("currency", "SGD");
        cmd.Parameters.AddWithValue("sla_date", (object?)extraction.SubmissionDeadline?.UtcDateTime ?? request.ReceivedDateTime?.UtcDateTime.AddDays(3) ?? DateTime.UtcNow.AddDays(3));
        cmd.Parameters.AddWithValue("contact_name", (object?)extraction.ClientContactName ?? request.FromName ?? string.Empty);
        cmd.Parameters.AddWithValue("contact_email", (object?)request.FromAddress ?? string.Empty);
        cmd.Parameters.AddWithValue("requirement_summary", (object?)extraction.RequirementSummary ?? request.BodyPreview ?? string.Empty);
        cmd.Parameters.AddWithValue("requirement_asked", (object?)extraction.RequirementAsked ?? request.JobDescriptionText ?? request.BodyText ?? string.Empty);
        cmd.Parameters.AddWithValue("gaps", string.Join(" | ", extraction.Gaps));
        cmd.Parameters.AddWithValue("status", "New");
        cmd.Parameters.AddWithValue("source_email_id", (object?)request.MessageId ?? string.Empty);
        cmd.Parameters.AddWithValue("conversation_id", (object?)request.ConversationId ?? string.Empty);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task InsertPlannerTaskAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int plannerId, string plannerNo,
        string clientName, string role, string priority, decimal budget, decimal? budgetMax, DateTime receivedOn, DateTime slaDate,
        string status, int openPositions, string sourceType, string contactName, string contactEmail, string requirementAsked,
        string skillsJson, string gapsJson, string timelineJson, string requirementTitle, string category, string notes,
        string secondarySkillsJson, string experienceRequired, string location, string workMode, string employmentType,
        CancellationToken cancellationToken)
    {
        const string sql = @"
insert into public.planner_task
(planner_no, client_name, role, priority, budget, currency, received_on, sla_date, status, open_positions,
 source_type, contact_name, contact_email, requirement_asked, skills_json, gaps_json, timeline_json,
 recommended_candidate_ids_json, assigned_vendor_ids_json, requirement_title, category, budget_max,
 internal_notes, secondary_skills_json, experience_required, location, work_mode, employment_type)
values
(@planner_no, @client_name, @role, @priority, @budget, 'SGD', @received_on, @sla_date, @status, @open_positions,
 @source_type, @contact_name, @contact_email, @requirement_asked, @skills_json::jsonb, @gaps_json::jsonb, @timeline_json::jsonb,
 '[]'::jsonb, '[]'::jsonb, @requirement_title, @category, @budget_max,
 @internal_notes, @secondary_skills_json::jsonb, @experience_required, @location, @work_mode, @employment_type);";

        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("planner_no", plannerNo);
        cmd.Parameters.AddWithValue("client_name", clientName);
        cmd.Parameters.AddWithValue("role", role);
        cmd.Parameters.AddWithValue("priority", priority);
        cmd.Parameters.AddWithValue("budget", NpgsqlDbType.Numeric, budget);
        cmd.Parameters.AddWithValue("received_on", receivedOn);
        cmd.Parameters.AddWithValue("sla_date", slaDate);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("open_positions", openPositions);
        cmd.Parameters.AddWithValue("source_type", sourceType);
        cmd.Parameters.AddWithValue("contact_name", contactName);
        cmd.Parameters.AddWithValue("contact_email", contactEmail);
        cmd.Parameters.AddWithValue("requirement_asked", requirementAsked);
        cmd.Parameters.AddWithValue("skills_json", skillsJson);
        cmd.Parameters.AddWithValue("gaps_json", gapsJson);
        cmd.Parameters.AddWithValue("timeline_json", timelineJson);
        cmd.Parameters.AddWithValue("requirement_title", requirementTitle);
        cmd.Parameters.AddWithValue("category", category);
        cmd.Parameters.AddWithValue("budget_max", NpgsqlDbType.Numeric, (object?)budgetMax ?? DBNull.Value);
        cmd.Parameters.AddWithValue("internal_notes", notes);
        cmd.Parameters.AddWithValue("secondary_skills_json", secondarySkillsJson);
        cmd.Parameters.AddWithValue("experience_required", experienceRequired);
        cmd.Parameters.AddWithValue("location", location);
        cmd.Parameters.AddWithValue("work_mode", workMode);
        cmd.Parameters.AddWithValue("employment_type", employmentType);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertPlannerEmailAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int plannerId,
        CreatePlannerFromMailRequest request, CancellationToken cancellationToken)
    {
        const string sql = @"
insert into public.planner_email
(planner_id, message_id, conversation_id, subject, from_email, received_on, email_body, is_processed)
values
(@planner_id, @message_id, @conversation_id, @subject, @from_email, @received_on, @email_body, true);";

        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("planner_id", plannerId);
        cmd.Parameters.AddWithValue("message_id", request.MessageId);
        cmd.Parameters.AddWithValue("conversation_id", (object?)request.ConversationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("subject", request.Subject);
        cmd.Parameters.AddWithValue("from_email", request.FromAddress);
        cmd.Parameters.AddWithValue("received_on", (object?)request.ReceivedDateTime?.UtcDateTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("email_body", request.BodyText);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertPlannerAttachmentsAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int plannerId,
        IEnumerable<CreatePlannerAttachmentDto> attachments, CancellationToken cancellationToken)
    {
        const string sql = @"
insert into public.planner_attachment
(planner_id, file_name, file_path, content_type)
values
(@planner_id, @file_name, @file_path, @content_type);";

        foreach (var attachment in attachments)
        {
            await using var cmd = new NpgsqlCommand(sql, connection, tx);
            cmd.Parameters.AddWithValue("planner_id", plannerId);
            cmd.Parameters.AddWithValue("file_name", attachment.FileName);
            cmd.Parameters.AddWithValue("file_path", attachment.SavedFilePath);
            cmd.Parameters.AddWithValue("content_type", attachment.ContentType);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertPlannerActivitiesAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int plannerId,
        CreatePlannerFromMailRequest request, PlannerMailExtraction extraction, CancellationToken cancellationToken)
    {
        const string sql = @"
insert into public.planner_activity
(planner_id, action_type, action_by, remarks)
values
(@planner_id, @action_type, @action_by, @remarks);";

        var activities = new List<(string actionType, string actionBy, string remarks)>
        {
            ("Task created", "Mailbox Worker", "Planner task created from mailbox extraction."),
            ("Mail received", FirstNotBlank(request.FromName, request.FromAddress, "Mailbox"), $"Subject: {request.Subject}")
        };

        if (!string.IsNullOrWhiteSpace(extraction.SubmissionDeadlineText))
        {
            activities.Add(("Submission deadline", FirstNotBlank(extraction.ClientName, "Client"), extraction.SubmissionDeadlineText!));
        }

        foreach (var activity in activities)
        {
            await using var cmd = new NpgsqlCommand(sql, connection, tx);
            cmd.Parameters.AddWithValue("planner_id", plannerId);
            cmd.Parameters.AddWithValue("action_type", activity.actionType);
            cmd.Parameters.AddWithValue("action_by", activity.actionBy);
            cmd.Parameters.AddWithValue("remarks", activity.remarks);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static string FirstNotBlank(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}
