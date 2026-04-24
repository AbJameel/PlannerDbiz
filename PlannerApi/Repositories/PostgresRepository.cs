
using IDataRecord = System.Data.IDataRecord;
using CommandBehavior = System.Data.CommandBehavior;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using PlannerApi.DTOs;
using PlannerApi.Models;
using RuleModel = PlannerApi.Models.Rule;

namespace PlannerApi.Repositories;

public class PostgresRepository(IConfiguration configuration, ILogger<PostgresRepository> logger) : IPostgresRepository
{
    private readonly string _connectionString = configuration.GetConnectionString("PlannerDb")
        ?? throw new InvalidOperationException("Connection string 'PlannerDb' not found.");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public Task<IReadOnlyList<PlannerTask>> GetTasksAsync() => ReadListAsync(TaskSelectSql + " order by received_on desc;", MapTask);

    public Task<IReadOnlyList<PlannerTask>> GetReviewQueueAsync() => ReadListAsync(TaskSelectSql + " where lower(status) in ('new','in review') order by received_on desc;", MapTask);

    public async Task<PlannerTask?> GetTaskAsync(int id)
    {
        var sql = TaskSelectSql + " where id = @id;";
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        return await reader.ReadAsync() ? MapTask(reader) : null;
    }

    public Task<IReadOnlyList<Candidate>> GetCandidatesAsync() => ReadListAsync(@"
            select candidate_id as id, candidate_name as name, candidate_current_role as current_role,
                   expected_budget, experience_years, notice_period, resume_file, skills_json::text, location
            from candidate order by candidate_id;", MapCandidate);

    public Task<IReadOnlyList<RuleModel>> GetRulesAsync() => ReadListAsync(@"
            select rule_id as id, rule_name as name, rule_type as category,
                   condition_json::text as condition, message as outcome, is_active
            from rule_master order by rule_id;", MapRule);

    public Task<IReadOnlyList<Vendor>> GetVendorsAsync() => ReadListAsync(@"
            select vendor_id as id, vendor_name as name, email,
                   supported_roles as coverage_roles, budget_min, budget_max,
                   case when is_active then 'Active' else 'Inactive' end as status
            from vendor order by vendor_id;", MapVendor);

    public async Task<IReadOnlyList<Vendor>> GetRecommendedVendorsAsync(int taskId)
    {
        var task = await GetTaskAsync(taskId);
        if (task is null) return [];
        var all = await GetVendorsAsync();
        return all.Where(v =>
                v.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(task.Role) || v.CoverageRoles.Contains(task.Role, StringComparison.OrdinalIgnoreCase)
                 || v.CoverageRoles.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase) && task.Role.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase)) &&
                (task.Budget <= 0 || (v.BudgetMin <= task.Budget && v.BudgetMax >= (task.BudgetMax ?? task.Budget))))
            .Take(5)
            .ToList();
    }

    public Task<IReadOnlyList<MailboxItem>> GetMailboxAsync() => ReadListAsync(@"
            select id, subject, from_email, received_on, snippet, is_read, source_type
            from mailbox_item order by received_on desc;", MapMailbox);

    public async Task<DashboardSummary> GetSummaryAsync()
    {
        const string sql = @"
            select 
                count(*) filter (where lower(status) = 'new') as new_tasks,
                count(*) filter (where lower(status) = 'in review') as under_review,
                count(*) filter (where lower(status) like 'assigned%') as assigned_to_vendors,
                count(*) filter (where sla_date::date = current_date) as closing_today
            from planner_task;";
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        if (!await reader.ReadAsync()) return new DashboardSummary();
        return new DashboardSummary
        {
            NewTasks = Convert.ToInt32(reader.GetInt64(0)),
            UnderReview = Convert.ToInt32(reader.GetInt64(1)),
            AssignedToVendors = Convert.ToInt32(reader.GetInt64(2)),
            ClosingToday = Convert.ToInt32(reader.GetInt64(3))
        };
    }

    public async Task<IReadOnlyList<Candidate>> GetRecommendedCandidatesAsync(int taskId)
    {
        var task = await GetTaskAsync(taskId);
        if (task is null)
            return [];

        var all = await GetCandidatesAsync();
        IEnumerable<Candidate> recommended = all;

        if (!string.IsNullOrWhiteSpace(task.Role))
            recommended = recommended.Where(c =>
                c.CurrentRole.Contains(task.Role, StringComparison.OrdinalIgnoreCase)
                || task.Role.Contains(c.CurrentRole, StringComparison.OrdinalIgnoreCase)
                || task.Role.Split(' ').Any(x => c.CurrentRole.Contains(x, StringComparison.OrdinalIgnoreCase)));

        if (task.Budget > 0)
            recommended = recommended.Where(c => c.ExpectedBudget <= (task.BudgetMax ?? task.Budget));

        if (task.Skills.Count > 0)
            recommended = recommended.OrderByDescending(c => c.Skills.Count(s => task.Skills.Any(x => x.Equals(s, StringComparison.OrdinalIgnoreCase))));
        else
            recommended = recommended.OrderByDescending(c => c.ExperienceYears);

        return recommended.Take(5).ToList();
    }

    public async Task<int> CreateTaskAsync(string subject, string fromEmail, string body, string? sourceType = null, string? fileName = null)
    {
        var role = GuessRole(subject, body);
        var client = "DBiz Internal";
        var budget = GuessBudget(body);
        var budgetMax = budget > 0 ? budget + 1500 : (decimal?)null;
        var openPositions = GuessOpenPositions(body);
        var skills = GuessSkills(body);
        var secondarySkills = GuessSecondarySkills(body, skills);
        var gaps = BuildGaps(role, budget, body);
        var priority = budget >= 9000 ? "High" : budget >= 6000 ? "Medium" : "Low";
        var plannerNo = $"PLN-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var slaDate = ExtractSlaDate(body) ?? DateTime.UtcNow.AddDays(3);
        var requirementTitle = !string.IsNullOrWhiteSpace(subject) ? subject : role;
        var category = GuessCategory(role, body);
        var notes = fileName is null ? "Auto-created from pasted content." : $"Auto-created from uploaded file {fileName}.";
        var timeline = new List<TaskTimelineItem>
        {
            new()
            {
                HappenedOn = DateTime.UtcNow,
                Title = "Task created",
                Description = "Planner task created from uploaded or pasted email/JD.",
                PerformedBy = "System"
            }
        };

        const string insertTask = @"
            insert into planner_task
            (planner_no, client_name, requirement_title, role, category, priority, budget, budget_max, currency, received_on, sla_date, status, open_positions,
             source_type, contact_name, contact_email, contact_phone, requirement_asked, notes, skills_json, secondary_skills_json, gaps_json, timeline_json,
             experience_required, location, work_mode, employment_type, recruiter_override_comment, recommended_candidate_ids_json, assigned_vendor_ids_json)
            values
            (@planner_no, @client_name, @requirement_title, @role, @category, @priority, @budget, @budget_max, @currency, now(), @sla_date, 'New', @open_positions,
             @source_type, @contact_name, @contact_email, @contact_phone, @requirement_asked, @notes, @skills_json::jsonb, @secondary_skills_json::jsonb, @gaps_json::jsonb, @timeline_json::jsonb,
             @experience_required, @location, @work_mode, @employment_type, @recruiter_override_comment, '[]'::jsonb, '[]'::jsonb)
            returning id;";

        const string insertMailbox = @"insert into mailbox_item (subject, from_email, received_on, snippet, is_read, source_type)
            values (@subject, @from_email, now(), @snippet, false, @source_type);";

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await using var taskCmd = new NpgsqlCommand(insertTask, conn, tx);
            taskCmd.Parameters.AddWithValue("planner_no", plannerNo);
            taskCmd.Parameters.AddWithValue("client_name", client);
            taskCmd.Parameters.AddWithValue("requirement_title", requirementTitle);
            taskCmd.Parameters.AddWithValue("role", role);
            taskCmd.Parameters.AddWithValue("category", category);
            taskCmd.Parameters.AddWithValue("priority", priority);
            taskCmd.Parameters.AddWithValue("budget", budget);
            taskCmd.Parameters.AddWithValue("budget_max", (object?)budgetMax ?? DBNull.Value);
            taskCmd.Parameters.AddWithValue("currency", "SGD");
            taskCmd.Parameters.AddWithValue("sla_date", slaDate);
            taskCmd.Parameters.AddWithValue("open_positions", openPositions);
            taskCmd.Parameters.AddWithValue("source_type", sourceType ?? "Manual Paste");
            taskCmd.Parameters.AddWithValue("contact_name", "Internal Request");
            taskCmd.Parameters.AddWithValue("contact_email", string.IsNullOrWhiteSpace(fromEmail) ? "internal@dbiz.com" : fromEmail);
            taskCmd.Parameters.AddWithValue("contact_phone", "");
            taskCmd.Parameters.AddWithValue("requirement_asked", Summarize(body));
            taskCmd.Parameters.AddWithValue("notes", notes);
            taskCmd.Parameters.AddWithValue("skills_json", JsonSerializer.Serialize(skills));
            taskCmd.Parameters.AddWithValue("secondary_skills_json", JsonSerializer.Serialize(secondarySkills));
            taskCmd.Parameters.AddWithValue("gaps_json", JsonSerializer.Serialize(gaps));
            taskCmd.Parameters.AddWithValue("timeline_json", JsonSerializer.Serialize(timeline));
            taskCmd.Parameters.AddWithValue("experience_required", GuessExperience(body));
            taskCmd.Parameters.AddWithValue("location", GuessLocation(body));
            taskCmd.Parameters.AddWithValue("work_mode", GuessWorkMode(body));
            taskCmd.Parameters.AddWithValue("employment_type", GuessEmploymentType(body));
            taskCmd.Parameters.AddWithValue("recruiter_override_comment", "");
            var taskId = Convert.ToInt32(await taskCmd.ExecuteScalarAsync());

            await using var mailboxCmd = new NpgsqlCommand(insertMailbox, conn, tx);
            mailboxCmd.Parameters.AddWithValue("subject", requirementTitle);
            mailboxCmd.Parameters.AddWithValue("from_email", string.IsNullOrWhiteSpace(fromEmail) ? "internal@dbiz.com" : fromEmail);
            mailboxCmd.Parameters.AddWithValue("snippet", Summarize(body));
            mailboxCmd.Parameters.AddWithValue("source_type", sourceType ?? "Manual Paste");
            await mailboxCmd.ExecuteNonQueryAsync();

            var recommendedIds = (await FindRecommendedCandidateIdsAsync(conn, tx, role, budgetMax ?? budget, skills)).ToList();
            if (recommendedIds.Count > 0)
            {
                const string updateSql = "update planner_task set recommended_candidate_ids_json = @ids::jsonb where id = @id;";
                await using var updateCmd = new NpgsqlCommand(updateSql, conn, tx);
                updateCmd.Parameters.AddWithValue("ids", JsonSerializer.Serialize(recommendedIds));
                updateCmd.Parameters.AddWithValue("id", taskId);
                await updateCmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
            return taskId;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            logger.LogError(ex, "Error creating task.");
            throw;
        }
    }

    public async Task UpdateTaskAsync(int id, UpdateTaskRequest request, string performedBy)
    {
        const string sql = @"
            update planner_task set
                client_name = @client_name,
                requirement_title = @requirement_title,
                role = @role,
                category = @category,
                priority = @priority,
                budget = @budget,
                budget_max = @budget_max,
                currency = @currency,
                sla_date = @sla_date,
                status = @status,
                open_positions = @open_positions,
                contact_name = @contact_name,
                contact_email = @contact_email,
                contact_phone = @contact_phone,
                requirement_asked = @requirement_asked,
                notes = @notes,
                skills_json = @skills_json::jsonb,
                secondary_skills_json = @secondary_skills_json::jsonb,
                experience_required = @experience_required,
                location = @location,
                work_mode = @work_mode,
                employment_type = @employment_type,
                recruiter_override_comment = @recruiter_override_comment,
                timeline_json = @timeline_json::jsonb
            where id = @id;";

        var existing = await GetTaskAsync(id) ?? throw new InvalidOperationException("Task not found.");
        var timeline = existing.Timeline;
        timeline.Add(new TaskTimelineItem
        {
            HappenedOn = DateTime.UtcNow,
            Title = "Task updated",
            Description = "Recruiter reviewed and updated the task details.",
            PerformedBy = performedBy
        });

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("client_name", request.ClientName);
        cmd.Parameters.AddWithValue("requirement_title", request.RequirementTitle);
        cmd.Parameters.AddWithValue("role", request.Role);
        cmd.Parameters.AddWithValue("category", request.Category);
        cmd.Parameters.AddWithValue("priority", request.Priority);
        cmd.Parameters.AddWithValue("budget", request.Budget);
        cmd.Parameters.AddWithValue("budget_max", (object?)request.BudgetMax ?? DBNull.Value);
        cmd.Parameters.AddWithValue("currency", request.Currency);
        cmd.Parameters.AddWithValue("sla_date", request.SlaDate);
        cmd.Parameters.AddWithValue("status", request.Status);
        cmd.Parameters.AddWithValue("open_positions", request.OpenPositions);
        cmd.Parameters.AddWithValue("contact_name", request.ContactName ?? "");
        cmd.Parameters.AddWithValue("contact_email", request.ContactEmail ?? "");
        cmd.Parameters.AddWithValue("contact_phone", request.ContactPhone ?? "");
        cmd.Parameters.AddWithValue("requirement_asked", request.RequirementAsked ?? "");
        cmd.Parameters.AddWithValue("notes", request.Notes ?? "");
        cmd.Parameters.AddWithValue("skills_json", JsonSerializer.Serialize(request.Skills ?? []));
        cmd.Parameters.AddWithValue("secondary_skills_json", JsonSerializer.Serialize(request.SecondarySkills ?? []));
        cmd.Parameters.AddWithValue("experience_required", request.ExperienceRequired ?? "");
        cmd.Parameters.AddWithValue("location", request.Location ?? "");
        cmd.Parameters.AddWithValue("work_mode", request.WorkMode ?? "");
        cmd.Parameters.AddWithValue("employment_type", request.EmploymentType ?? "");
        cmd.Parameters.AddWithValue("recruiter_override_comment", request.RecruiterOverrideComment ?? "");
        cmd.Parameters.AddWithValue("timeline_json", JsonSerializer.Serialize(timeline));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task AssignVendorsAsync(int id, AssignVendorsRequest request, string performedBy)
    {
        if (request.VendorIds.Count == 0) throw new InvalidOperationException("At least one vendor must be selected.");

        var existing = await GetTaskAsync(id) ?? throw new InvalidOperationException("Task not found.");
        var assignedIds = request.VendorIds.Distinct().ToList();
        existing.Timeline.Add(new TaskTimelineItem
        {
            HappenedOn = DateTime.UtcNow,
            Title = "Assigned to vendor",
            Description = string.IsNullOrWhiteSpace(request.AssignmentNote) ? "Task assigned to selected vendors." : request.AssignmentNote,
            PerformedBy = performedBy
        });

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        const string deleteSql = "delete from planner_vendor_assignment where planner_id = @planner_id;";
        await using (var del = new NpgsqlCommand(deleteSql, conn, tx))
        {
            del.Parameters.AddWithValue("planner_id", id);
            await del.ExecuteNonQueryAsync();
        }

        const string insertSql = @"insert into planner_vendor_assignment (planner_id, vendor_id, assignment_note, assigned_by_name, assigned_on, status)
                                   values (@planner_id, @vendor_id, @assignment_note, @assigned_by_name, now(), 'Assigned');";
        foreach (var vendorId in assignedIds)
        {
            await using var ins = new NpgsqlCommand(insertSql, conn, tx);
            ins.Parameters.AddWithValue("planner_id", id);
            ins.Parameters.AddWithValue("vendor_id", vendorId);
            ins.Parameters.AddWithValue("assignment_note", request.AssignmentNote ?? "");
            ins.Parameters.AddWithValue("assigned_by_name", performedBy);
            await ins.ExecuteNonQueryAsync();
        }

        const string updateTaskSql = @"update planner_task
                                       set assigned_vendor_ids_json = @ids::jsonb,
                                           status = @status,
                                           timeline_json = @timeline_json::jsonb
                                       where id = @id;";
        await using (var upd = new NpgsqlCommand(updateTaskSql, conn, tx))
        {
            upd.Parameters.AddWithValue("ids", JsonSerializer.Serialize(assignedIds));
            upd.Parameters.AddWithValue("status", request.UpdateStatusTo);
            upd.Parameters.AddWithValue("timeline_json", JsonSerializer.Serialize(existing.Timeline));
            upd.Parameters.AddWithValue("id", id);
            await upd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    public async Task<int> CreateRuleAsync(RuleModel rule)
    {
        const string sql = @"insert into rule_master (rule_name, rule_type, condition_json, message, is_active)
            values (@name, @category, @condition::jsonb, @outcome, @is_active) returning rule_id;";
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", rule.Name);
        cmd.Parameters.AddWithValue("category", rule.Category);
        cmd.Parameters.AddWithValue("condition", string.IsNullOrWhiteSpace(rule.Condition) ? "{}" : rule.Condition);
        cmd.Parameters.AddWithValue("outcome", rule.Outcome);
        cmd.Parameters.AddWithValue("is_active", rule.IsActive);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int> CreateVendorAsync(Vendor vendor)
    {
        const string sql = @"insert into vendor (vendor_name, email, supported_roles, budget_min, budget_max, is_active)
            values (@name, @email, @coverage_roles, @budget_min, @budget_max, @is_active) returning vendor_id;";
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", vendor.Name);
        cmd.Parameters.AddWithValue("email", vendor.Email);
        cmd.Parameters.AddWithValue("coverage_roles", vendor.CoverageRoles);
        cmd.Parameters.AddWithValue("budget_min", vendor.BudgetMin);
        cmd.Parameters.AddWithValue("budget_max", vendor.BudgetMax);
        cmd.Parameters.AddWithValue("is_active", vendor.Status.Equals("Active", StringComparison.OrdinalIgnoreCase));
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int> CreateCandidateAsync(Candidate candidate)
    {
        const string sql = @"insert into candidate (candidate_name, candidate_current_role, expected_budget, experience_years, notice_period, resume_file, skills_json, location)
            values (@name, @current_role, @expected_budget, @experience_years, @notice_period, @resume_file, @skills_json::jsonb, @location) returning candidate_id;";
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", candidate.Name);
        cmd.Parameters.AddWithValue("current_role", candidate.CurrentRole);
        cmd.Parameters.AddWithValue("expected_budget", candidate.ExpectedBudget);
        cmd.Parameters.AddWithValue("experience_years", candidate.ExperienceYears);
        cmd.Parameters.AddWithValue("notice_period", candidate.NoticePeriod);
        cmd.Parameters.AddWithValue("resume_file", candidate.ResumeFile);
        cmd.Parameters.AddWithValue("skills_json", JsonSerializer.Serialize(candidate.Skills));
        cmd.Parameters.AddWithValue("location", candidate.Location);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<(string VendorComment, IReadOnlyList<VendorCandidateSubmission> Items)> GetVendorSubmissionsAsync(int taskId, int? vendorId = null)
    {
        var task = await GetTaskAsync(taskId);
        var vendorComment = task?.Notes ?? string.Empty;
        var sql = @"select submission_id, planner_id, vendor_id, candidate_name, contact_detail, visa_type, resume_file, candidate_status, is_submitted, created_on, updated_on from planner_candidate_submission where planner_id = @planner_id";
        if (vendorId.HasValue) sql += " and vendor_id = @vendor_id";
        sql += " order by submission_id;";
        var items = new List<VendorCandidateSubmission>();
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("planner_id", taskId);
        if (vendorId.HasValue) cmd.Parameters.AddWithValue("vendor_id", vendorId.Value);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new VendorCandidateSubmission
            {
                SubmissionId = reader.GetInt32(0),
                PlannerId = reader.GetInt32(1),
                VendorId = reader.GetInt32(2),
                CandidateName = reader.GetString(3),
                ContactDetail = reader.GetString(4),
                VisaType = reader.GetString(5),
                ResumeFile = reader.GetString(6),
                CandidateStatus = reader.GetString(7),
                IsSubmitted = reader.GetBoolean(8),
                CreatedOn = reader.GetDateTime(9),
                UpdatedOn = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
            });
        }
        return (vendorComment, items);
    }

    public async Task SaveVendorSubmissionsAsync(int taskId, int vendorId, SaveVendorCandidatesRequest request, string performedBy)
    {
        var task = await GetTaskAsync(taskId) ?? throw new InvalidOperationException("Task not found.");
        task.Timeline.Add(new TaskTimelineItem
        {
            HappenedOn = DateTime.UtcNow,
            Title = request.Submit ? "Vendor submitted candidates" : "Vendor saved draft",
            Description = request.Submit ? "Vendor submitted candidate list for recruiter review." : "Vendor saved candidate draft.",
            PerformedBy = performedBy
        });

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        const string deleteSql = "delete from planner_candidate_submission where planner_id = @planner_id and vendor_id = @vendor_id and is_submitted = false;";
        await using (var del = new NpgsqlCommand(deleteSql, conn, tx))
        {
            del.Parameters.AddWithValue("planner_id", taskId);
            del.Parameters.AddWithValue("vendor_id", vendorId);
            await del.ExecuteNonQueryAsync();
        }
        const string insSql = @"insert into planner_candidate_submission (planner_id, vendor_id, candidate_name, contact_detail, visa_type, resume_file, candidate_status, is_submitted, created_on, updated_on)
                                values (@planner_id,@vendor_id,@candidate_name,@contact_detail,@visa_type,@resume_file,@candidate_status,@is_submitted, now(), now());";
        foreach (var item in request.Items)
        {
            await using var ins = new NpgsqlCommand(insSql, conn, tx);
            ins.Parameters.AddWithValue("planner_id", taskId);
            ins.Parameters.AddWithValue("vendor_id", vendorId);
            ins.Parameters.AddWithValue("candidate_name", item.CandidateName ?? string.Empty);
            ins.Parameters.AddWithValue("contact_detail", item.ContactDetail ?? string.Empty);
            ins.Parameters.AddWithValue("visa_type", item.VisaType ?? string.Empty);
            ins.Parameters.AddWithValue("resume_file", item.ResumeFile ?? string.Empty);
            ins.Parameters.AddWithValue("candidate_status", request.Submit ? "Submitted" : "Draft");
            ins.Parameters.AddWithValue("is_submitted", request.Submit);
            await ins.ExecuteNonQueryAsync();
        }
        const string updTask = @"update planner_task set notes = @notes, status = @status, timeline_json = @timeline_json::jsonb where id = @id;";
        await using (var upd = new NpgsqlCommand(updTask, conn, tx))
        {
            upd.Parameters.AddWithValue("notes", request.VendorComment ?? string.Empty);
            upd.Parameters.AddWithValue("status", request.Submit ? "Vendor Submitted" : task.Status);
            upd.Parameters.AddWithValue("timeline_json", JsonSerializer.Serialize(task.Timeline));
            upd.Parameters.AddWithValue("id", taskId);
            await upd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    private async Task<IReadOnlyList<T>> ReadListAsync<T>(string sql, Func<IDataRecord, T> mapper)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var items = new List<T>();
        while (await reader.ReadAsync()) items.Add(mapper(reader));
        return items;
    }

    private const string TaskSelectSql = @"
        select id, planner_no, client_name, requirement_title, role, category, priority, budget, budget_max, currency, received_on, sla_date,
               status, open_positions, source_type, contact_name, contact_email, contact_phone, requirement_asked, notes,
               skills_json::text, secondary_skills_json::text, gaps_json::text, experience_required, location, work_mode, employment_type, recruiter_override_comment,
               timeline_json::text, recommended_candidate_ids_json::text, assigned_vendor_ids_json::text
        from planner_task";

    private static PlannerTask MapTask(IDataRecord record) => new()
    {
        Id = record.GetInt32(0),
        PlannerNo = record.GetString(1),
        ClientName = record.GetString(2),
        RequirementTitle = record.GetString(3),
        Role = record.GetString(4),
        Category = record.GetString(5),
        Priority = record.GetString(6),
        Budget = record.GetDecimal(7),
        BudgetMax = record.IsDBNull(8) ? null : record.GetDecimal(8),
        Currency = record.GetString(9),
        ReceivedOn = record.GetDateTime(10),
        SlaDate = record.GetDateTime(11),
        Status = record.GetString(12),
        OpenPositions = record.GetInt32(13),
        SourceType = record.GetString(14),
        ContactName = record.GetString(15),
        ContactEmail = record.GetString(16),
        ContactPhone = record.GetString(17),
        RequirementAsked = record.GetString(18),
        Notes = record.GetString(19),
        Skills = DeserializeList<string>(record.GetString(20)),
        SecondarySkills = DeserializeList<string>(record.GetString(21)),
        Gaps = DeserializeList<string>(record.GetString(22)),
        ExperienceRequired = record.GetString(23),
        Location = record.GetString(24),
        WorkMode = record.GetString(25),
        EmploymentType = record.GetString(26),
        RecruiterOverrideComment = record.GetString(27),
        Timeline = DeserializeList<TaskTimelineItem>(record.GetString(28)),
        RecommendedCandidateIds = DeserializeList<int>(record.GetString(29)),
        AssignedVendorIds = DeserializeList<int>(record.GetString(30))
    };

    private static Candidate MapCandidate(IDataRecord record) => new()
    {
        Id = record.GetInt32(0),
        Name = record.GetString(1),
        CurrentRole = record.GetString(2),
        ExpectedBudget = record.GetDecimal(3),
        ExperienceYears = record.GetInt32(4),
        NoticePeriod = record.GetString(5),
        ResumeFile = record.GetString(6),
        Skills = DeserializeList<string>(record.GetString(7)),
        Location = record.GetString(8)
    };

    private static RuleModel MapRule(IDataRecord record) => new() { Id = record.GetInt32(0), Name = record.GetString(1), Category = record.GetString(2), Condition = record.GetString(3), Outcome = record.GetString(4), IsActive = record.GetBoolean(5) };
    private static Vendor MapVendor(IDataRecord record) => new() { Id = record.GetInt32(0), Name = record.GetString(1), Email = record.GetString(2), CoverageRoles = record.GetString(3), BudgetMin = record.GetDecimal(4), BudgetMax = record.GetDecimal(5), Status = record.GetString(6) };
    private static MailboxItem MapMailbox(IDataRecord record) => new() { Id = record.GetInt32(0), Subject = record.GetString(1), FromEmail = record.GetString(2), ReceivedOn = record.GetDateTime(3), Snippet = record.GetString(4), IsRead = record.GetBoolean(5), SourceType = record.GetString(6) };
    private static List<T> DeserializeList<T>(string json) => JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];

    private static string Summarize(string input)
    {
        input = Regex.Replace(input ?? string.Empty, @"\r?\n", " ").Trim();
        return input.Length <= 260 ? input : input[..260] + "...";
    }

    private static string GuessRole(string subject, string body)
    {
        var content = $"{subject}\n{body}";
        var m = Regex.Match(content, @"Job\s*Title\s*:?\s*(.+)", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value.Trim();
        if (content.Contains("IT Infrastructure Engineer", StringComparison.OrdinalIgnoreCase)) return "IT Infrastructure Engineer";
        if (content.Contains("Business Analyst", StringComparison.OrdinalIgnoreCase)) return "Business Analyst";
        if (content.Contains("Azure", StringComparison.OrdinalIgnoreCase)) return "Azure Cloud Engineer";
        if (content.Contains("React", StringComparison.OrdinalIgnoreCase)) return "React Developer";
        if (content.Contains("DevOps", StringComparison.OrdinalIgnoreCase)) return "DevOps Engineer";
        if (content.Contains("QA", StringComparison.OrdinalIgnoreCase)) return "QA Engineer";
        return string.IsNullOrWhiteSpace(subject) ? "Technical Consultant" : subject.Trim();
    }

    private static decimal GuessBudget(string body)
    {
        var match = Regex.Match(body ?? string.Empty, @"(?:SGD|S\$|\$)?\s*(\d{4,5})(?:\s|/|$)");
        return match.Success && decimal.TryParse(match.Groups[1].Value, out var value) ? value : 6000m;
    }

    private static int GuessOpenPositions(string body)
    {
        var match = Regex.Match(body ?? string.Empty, @"(?:need|require|looking for)\s+(\d+)", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : 1;
    }

    private static List<string> GuessSkills(string body)
    {
        var skills = new[]
        {
            "Azure", "Terraform", "CI/CD", "React", "TypeScript", "Docker",
            "Kubernetes", "SQL", "PostgreSQL", "Business Analysis", "Testing",
            "Microsoft 365", "Windows OS", "Networking", "Servers", "Jira",
            "MS Project", "Asana", "Risk Management", "AV Equipment"
        };

        return skills.Where(skill => body.Contains(skill, StringComparison.OrdinalIgnoreCase))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Take(8)
                     .ToList();
    }

    private static List<string> GuessSecondarySkills(string body, List<string> primary)
    {
        var all = GuessSkills(body);
        return all.Where(x => !primary.Contains(x, StringComparer.OrdinalIgnoreCase)).Take(5).ToList();
    }

    private static List<string> BuildGaps(string role, decimal budget, string body)
    {
        var gaps = new List<string>();
        if (budget <= 0) gaps.Add("Budget missing.");
        else if (budget < 4500) gaps.Add("Budget below preferred vendor threshold.");
        if (string.IsNullOrWhiteSpace(role))
            gaps.Add("Role needs manual review.");
        if (!body.Contains("sla", StringComparison.OrdinalIgnoreCase) &&
            !body.Contains("deadline", StringComparison.OrdinalIgnoreCase) &&
            !Regex.IsMatch(body, @"\b\d{1,2}[/-]\d{1,2}[/-]\d{2,4}\b"))
            gaps.Add("SLA date not clearly mentioned. Default SLA +3 days applied.");
        gaps.Add("Client defaulted to DBiz Internal.");
        return gaps.Distinct().ToList();
    }

    private static string GuessExperience(string body)
    {
        var match = Regex.Match(body ?? string.Empty, @"Minimum\s+(\d+)\s+years", RegexOptions.IgnoreCase);
        return match.Success ? $"{match.Groups[1].Value}+ years" : "";
    }

    private static string GuessCategory(string role, string body)
    {
        var content = $"{role} {body}";
        if (content.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase) || content.Contains("Microsoft 365", StringComparison.OrdinalIgnoreCase)) return "Infrastructure";
        if (content.Contains("React", StringComparison.OrdinalIgnoreCase)) return "Development";
        if (content.Contains("Business Analyst", StringComparison.OrdinalIgnoreCase)) return "Functional";
        if (content.Contains("QA", StringComparison.OrdinalIgnoreCase)) return "Testing";
        return "General";
    }

    private static string GuessLocation(string body)
    {
        if (body.Contains("Singapore", StringComparison.OrdinalIgnoreCase)) return "Singapore";
        return "";
    }

    private static string GuessWorkMode(string body)
    {
        if (body.Contains("Hybrid", StringComparison.OrdinalIgnoreCase)) return "Hybrid";
        if (body.Contains("Onsite", StringComparison.OrdinalIgnoreCase) || body.Contains("On-site", StringComparison.OrdinalIgnoreCase)) return "Onsite";
        if (body.Contains("Remote", StringComparison.OrdinalIgnoreCase)) return "Remote";
        return "";
    }

    private static string GuessEmploymentType(string body)
    {
        if (body.Contains("Consultant", StringComparison.OrdinalIgnoreCase)) return "Consultant";
        if (body.Contains("Contract", StringComparison.OrdinalIgnoreCase)) return "Contract";
        return "";
    }

    private static DateTime? ExtractSlaDate(string body)
    {
        var match = Regex.Match(body ?? string.Empty, @"\b(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})\b");
        if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var dt))
            return dt;
        return null;
    }

    private static async Task<List<int>> FindRecommendedCandidateIdsAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, string role, decimal budget, List<string> skills)
    {
        const string sql = @"
            select candidate_id as id, candidate_current_role as current_role, expected_budget, skills_json::text
            from candidate
            order by expected_budget asc, experience_years desc;";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        await using var reader = await cmd.ExecuteReaderAsync();
        var recommended = new List<(int Id, int Score)>();
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var currentRole = reader.GetString(1);
            var expectedBudget = reader.GetDecimal(2);
            var candidateSkills = DeserializeList<string>(reader.GetString(3));
            var score = 0;
            if (currentRole.Contains(role, StringComparison.OrdinalIgnoreCase) || role.Contains(currentRole, StringComparison.OrdinalIgnoreCase))
                score += 4;
            if (expectedBudget <= budget || budget <= 0)
                score += 3;
            score += candidateSkills.Count(s => skills.Any(x => x.Equals(s, StringComparison.OrdinalIgnoreCase)));
            if (score > 0) recommended.Add((id, score));
        }
        await reader.CloseAsync();
        return recommended.OrderByDescending(x => x.Score).ThenBy(x => x.Id).Take(5).Select(x => x.Id).ToList();
    }

    public async Task<(IReadOnlyList<PlannerListItem> Items, int TotalCount)> GetPlannerListAsync(
    string? status,
    string? priority,
    string? search,
    string? role,
    string? clientName,
    bool? closingToday,
    string? userRole,
    int? vendorId,
    string? slaDate)
    {
        var sql = """
        SELECT
            p.id,
            p.planner_no,
            p.client_name,
            COALESCE(p.requirement_title, '') AS requirement_title,
            p.role,
            p.status,
            p.priority,
            p.budget,
            p.currency,
            p.sla_date,
            p.open_positions
        FROM planner_task p
        WHERE 1 = 1
        """;

        var parameters = new Dictionary<string, object?>();

        if (userRole == "VENDOR" && vendorId.HasValue)
        {
            sql += """
            AND EXISTS (
                SELECT 1
                FROM planner_vendor_assignment va
                WHERE va.planner_id = p.id
                  AND va.vendor_id = @vendorId
            )
            """;
            parameters["vendorId"] = vendorId.Value;
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            sql += " AND p.status = @status ";
            parameters["status"] = status;
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            sql += " AND p.priority = @priority ";
            parameters["priority"] = priority;
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            sql += " AND LOWER(p.role) LIKE LOWER(@role) ";
            parameters["role"] = $"%{role}%";
        }

        if (!string.IsNullOrWhiteSpace(clientName))
        {
            sql += " AND LOWER(p.client_name) LIKE LOWER(@clientName) ";
            parameters["clientName"] = $"%{clientName}%";
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += " AND LOWER(COALESCE(p.requirement_asked, '')) LIKE LOWER(@search) ";
            parameters["search"] = $"%{search}%";
        }

        if (!string.IsNullOrWhiteSpace(slaDate))
        {
            sql += " AND DATE(p.sla_date) = DATE(@slaDate) ";
            parameters["slaDate"] = slaDate;
        }

        if (closingToday == true)
        {
            sql += " AND DATE(p.sla_date) = CURRENT_DATE ";
        }

        sql += " ORDER BY p.received_on DESC ";

        var items = await ReadListAsync(sql, reader => new PlannerListItem
        {
            Id = reader.GetInt32(0),
            PlannerNo = reader.GetString(1),
            ClientName = reader.GetString(2),
            RequirementTitle = reader.GetString(3),
            Role = reader.GetString(4),
            Status = reader.GetString(5),
            Priority = reader.GetString(6),
            Budget = reader.GetDecimal(7),
            Currency = reader.GetString(8),
            SlaDate = reader.GetDateTime(9),
            OpenPositions = reader.GetInt32(10)
        }, parameters);

        return (items, items.Count);
    }

    private async Task<List<T>> ReadListAsync<T>(
      string sql,
      Func<NpgsqlDataReader, T> map,
      Dictionary<string, object?> parameters)
    {
        var result = new List<T>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);

        foreach (var p in parameters)
        {
            cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
        }

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(map(reader));
        }

        return result;
    }
}
