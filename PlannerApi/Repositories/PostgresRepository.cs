using IDataRecord = System.Data.IDataRecord;
using CommandBehavior = System.Data.CommandBehavior;
using System.Text.Json;
using Npgsql;
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

    public async Task<IReadOnlyList<PlannerTask>> GetTasksAsync()
    {
        const string sql = @"
            select id, planner_no, client_name, role, priority, budget, currency, received_on, sla_date,
                   status, open_positions, source_type, contact_name, contact_email, requirement_asked,
                   skills_json::text, gaps_json::text, timeline_json::text, recommended_candidate_ids_json::text, assigned_vendor_ids_json::text
            from planner_task
            order by received_on desc;";

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var items = new List<PlannerTask>();
        while (await reader.ReadAsync())
        {
            items.Add(MapTask(reader));
        }

        return items;
    }

    public async Task<PlannerTask?> GetTaskAsync(int id)
    {
        const string sql = @"
            select id, planner_no, client_name, role, priority, budget, currency, received_on, sla_date,
                   status, open_positions, source_type, contact_name, contact_email, requirement_asked,
                   skills_json::text, gaps_json::text, timeline_json::text, recommended_candidate_ids_json::text, assigned_vendor_ids_json::text
            from planner_task
            where id = @id;";

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync();

        return await reader.ReadAsync() ? MapTask(reader) : null;
    }

    public async Task<IReadOnlyList<Candidate>> GetCandidatesAsync()
    {
        const string sql = @"
            select
                candidate_id as id,
                candidate_name as name,
                candidate_current_role as current_role,
                expected_budget,
                experience_years,
                notice_period,
                resume_file,
                skills_json::text,
                location
            from candidate
            order by candidate_id;";

        return await ReadListAsync(sql, MapCandidate);
    }

    public async Task<IReadOnlyList<RuleModel>> GetRulesAsync()
    {
        const string sql = @"
            select
                rule_id as id,
                rule_name as name,
                rule_type as category,
                condition_json::text as condition,
                message as outcome,
                is_active
            from rule_master
            order by rule_id;";

        return await ReadListAsync(sql, MapRule);
    }

    public async Task<IReadOnlyList<Vendor>> GetVendorsAsync()
    {
        const string sql = @"
            select
                vendor_id as id,
                vendor_name as name,
                email,
                supported_roles as coverage_roles,
                budget_min,
                budget_max,
                case when is_active then 'Active' else 'Inactive' end as status
            from vendor
            order by vendor_id;";

        return await ReadListAsync(sql, MapVendor);
    }

    public async Task<IReadOnlyList<MailboxItem>> GetMailboxAsync()
    {
        const string sql = @"
            select id, subject, from_email, received_on, snippet, is_read, source_type
            from mailbox_item
            order by received_on desc;";

        return await ReadListAsync(sql, MapMailbox);
    }

    public async Task<DashboardSummary> GetSummaryAsync()
    {
        const string sql = @"
            select 
                count(*) filter (where lower(status) = 'new') as new_tasks,
                count(*) filter (where lower(status) = 'under review') as under_review,
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
        if (task is null || task.RecommendedCandidateIds.Count == 0)
            return [];

        const string sql = @"
            select
                candidate_id as id,
                candidate_name as name,
                candidate_current_role as current_role,
                expected_budget,
                experience_years,
                notice_period,
                resume_file,
                skills_json::text,
                location
            from candidate
            where candidate_id = any(@ids)
            order by candidate_id;";

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ids", task.RecommendedCandidateIds.ToArray());
        await using var reader = await cmd.ExecuteReaderAsync();

        var items = new List<Candidate>();
        while (await reader.ReadAsync())
        {
            items.Add(MapCandidate(reader));
        }

        return items;
    }

    public async Task<int> CreateTaskAsync(string subject, string fromEmail, string body, string? sourceType = null)
    {
        var role = GuessRole(subject, body);
        var client = GuessClient(subject, body, fromEmail);
        var budget = GuessBudget(body);
        var openPositions = GuessOpenPositions(body);
        var skills = GuessSkills(body);
        var gaps = BuildGaps(role, budget, body);
        var priority = budget >= 9000 ? "High" : budget >= 6000 ? "Medium" : "Low";
        var plannerNo = $"PLN-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var slaDate = DateTime.UtcNow.AddDays(2);

        var timeline = new List<TaskTimelineItem>
        {
            new()
            {
                HappenedOn = DateTime.UtcNow,
                Title = "Task created",
                Description = "Planner task created from uploaded or pasted email.",
                PerformedBy = "System"
            }
        };

        const string insertTask = @"
            insert into planner_task
            (planner_no, client_name, role, priority, budget, currency, received_on, sla_date, status, open_positions,
             source_type, contact_name, contact_email, requirement_asked, skills_json, gaps_json, timeline_json,
             recommended_candidate_ids_json, assigned_vendor_ids_json)
            values
            (@planner_no, @client_name, @role, @priority, @budget, @currency, now(), @sla_date, 'New', @open_positions,
             @source_type, 'Mailbox Contact', @contact_email, @requirement_asked, @skills_json::jsonb, @gaps_json::jsonb, @timeline_json::jsonb,
             '[]'::jsonb, '[]'::jsonb)
            returning id;";

        const string insertMailbox = @"
            insert into mailbox_item (subject, from_email, received_on, snippet, is_read, source_type)
            values (@subject, @from_email, now(), @snippet, false, @source_type);";

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            await using var taskCmd = new NpgsqlCommand(insertTask, conn, tx);
            taskCmd.Parameters.AddWithValue("planner_no", plannerNo);
            taskCmd.Parameters.AddWithValue("client_name", client);
            taskCmd.Parameters.AddWithValue("role", role);
            taskCmd.Parameters.AddWithValue("priority", priority);
            taskCmd.Parameters.AddWithValue("budget", budget);
            taskCmd.Parameters.AddWithValue("currency", "SGD");
            taskCmd.Parameters.AddWithValue("sla_date", slaDate);
            taskCmd.Parameters.AddWithValue("open_positions", openPositions);
            taskCmd.Parameters.AddWithValue("source_type", sourceType ?? "Uploaded Mail");
            taskCmd.Parameters.AddWithValue("contact_email", fromEmail);
            taskCmd.Parameters.AddWithValue("requirement_asked", Summarize(body));
            taskCmd.Parameters.AddWithValue("skills_json", JsonSerializer.Serialize(skills));
            taskCmd.Parameters.AddWithValue("gaps_json", JsonSerializer.Serialize(gaps));
            taskCmd.Parameters.AddWithValue("timeline_json", JsonSerializer.Serialize(timeline));

            var taskId = Convert.ToInt32(await taskCmd.ExecuteScalarAsync());

            await using var mailboxCmd = new NpgsqlCommand(insertMailbox, conn, tx);
            mailboxCmd.Parameters.AddWithValue("subject", subject);
            mailboxCmd.Parameters.AddWithValue("from_email", fromEmail);
            mailboxCmd.Parameters.AddWithValue("snippet", Summarize(body));
            mailboxCmd.Parameters.AddWithValue("source_type", sourceType ?? "Uploaded Mail");
            await mailboxCmd.ExecuteNonQueryAsync();

            var recommendedIds = await FindRecommendedCandidateIdsAsync(conn, tx, role, budget, skills);
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

    public async Task<int> CreateRuleAsync(RuleModel rule)
    {
        const string sql = @"
            insert into rule_master
            (rule_name, rule_type, condition_json, message, is_active)
            values
            (@name, @category, @condition::jsonb, @outcome, @is_active)
            returning rule_id;";

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
        const string sql = @"
            insert into vendor
            (vendor_name, email, supported_roles, budget_min, budget_max, is_active)
            values
            (@name, @email, @coverage_roles, @budget_min, @budget_max, @is_active)
            returning vendor_id;";

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
        const string sql = @"
            insert into candidate
            (candidate_name, candidate_current_role, expected_budget, experience_years, notice_period, resume_file, skills_json, location)
            values
            (@name, @current_role, @expected_budget, @experience_years, @notice_period, @resume_file, @skills_json::jsonb, @location)
            returning candidate_id;";

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

    private async Task<IReadOnlyList<T>> ReadListAsync<T>(string sql, Func<IDataRecord, T> mapper)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var items = new List<T>();
        while (await reader.ReadAsync())
        {
            items.Add(mapper(reader));
        }

        return items;
    }

    private static PlannerTask MapTask(IDataRecord record)
    {
        return new PlannerTask
        {
            Id = record.GetInt32(0),
            PlannerNo = record.GetString(1),
            ClientName = record.GetString(2),
            Role = record.GetString(3),
            Priority = record.GetString(4),
            Budget = record.GetDecimal(5),
            Currency = record.GetString(6),
            ReceivedOn = record.GetDateTime(7),
            SlaDate = record.GetDateTime(8),
            Status = record.GetString(9),
            OpenPositions = record.GetInt32(10),
            SourceType = record.GetString(11),
            ContactName = record.GetString(12),
            ContactEmail = record.GetString(13),
            RequirementAsked = record.GetString(14),
            Skills = DeserializeList<string>(record.GetString(15)),
            Gaps = DeserializeList<string>(record.GetString(16)),
            Timeline = DeserializeList<TaskTimelineItem>(record.GetString(17)),
            RecommendedCandidateIds = DeserializeList<int>(record.GetString(18)),
            AssignedVendorIds = DeserializeList<int>(record.GetString(19))
        };
    }

    private static Candidate MapCandidate(IDataRecord record)
    {
        return new Candidate
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
    }

    private static RuleModel MapRule(IDataRecord record) => new()
    {
        Id = record.GetInt32(0),
        Name = record.GetString(1),
        Category = record.GetString(2),
        Condition = record.GetString(3),
        Outcome = record.GetString(4),
        IsActive = record.GetBoolean(5)
    };

    private static Vendor MapVendor(IDataRecord record) => new()
    {
        Id = record.GetInt32(0),
        Name = record.GetString(1),
        Email = record.GetString(2),
        CoverageRoles = record.GetString(3),
        BudgetMin = record.GetDecimal(4),
        BudgetMax = record.GetDecimal(5),
        Status = record.GetString(6)
    };

    private static MailboxItem MapMailbox(IDataRecord record) => new()
    {
        Id = record.GetInt32(0),
        Subject = record.GetString(1),
        FromEmail = record.GetString(2),
        ReceivedOn = record.GetDateTime(3),
        Snippet = record.GetString(4),
        IsRead = record.GetBoolean(5),
        SourceType = record.GetString(6)
    };

    private static List<T> DeserializeList<T>(string json)
        => JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];

    private static string Summarize(string input)
    {
        input = System.Text.RegularExpressions.Regex.Replace(input ?? string.Empty, @"\r?\n", " ").Trim();
        return input.Length <= 220 ? input : input[..220] + "...";
    }

    private static string GuessRole(string subject, string body)
    {
        var content = $"{subject} {body}".ToLowerInvariant();
        if (content.Contains("business analyst")) return "Business Analyst";
        if (content.Contains("azure")) return "Azure Cloud Engineer";
        if (content.Contains("react")) return "React Developer";
        if (content.Contains("devops")) return "DevOps Engineer";
        if (content.Contains("qa")) return "QA Engineer";
        return "Technical Consultant";
    }

    private static string GuessClient(string subject, string body, string fromEmail)
    {
        var lines = body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var clientLine = lines.FirstOrDefault(x => x.Contains("client", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(clientLine))
        {
            return clientLine.Replace("Client", "", StringComparison.OrdinalIgnoreCase).Trim(' ', ':', '-');
        }

        var domain = fromEmail.Split('@').Skip(1).FirstOrDefault()?.Split('.').FirstOrDefault();
        return string.IsNullOrWhiteSpace(domain) ? "Uploaded Client" : char.ToUpper(domain![0]) + domain[1..];
    }

    private static decimal GuessBudget(string body)
    {
        var match = System.Text.RegularExpressions.Regex.Match(body ?? string.Empty, @"(\d{4,5})(?:\s|/|$)");
        return match.Success && decimal.TryParse(match.Groups[1].Value, out var value) ? value : 6000m;
    }

    private static int GuessOpenPositions(string body)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            body ?? string.Empty,
            @"(?:need|require|looking for)\s+(\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : 1;
    }

    private static List<string> GuessSkills(string body)
    {
        var skills = new[]
        {
            "Azure", "Terraform", "CI/CD", "React", "TypeScript", "Docker",
            "Kubernetes", "SQL", "PostgreSQL", "Business Analysis", "Testing"
        };

        return skills.Where(skill => body.Contains(skill, StringComparison.OrdinalIgnoreCase))
                     .Take(6)
                     .ToList();
    }

    private static List<string> BuildGaps(string role, decimal budget, string body)
    {
        var gaps = new List<string>();
        if (budget < 4500) gaps.Add("Budget below preferred vendor threshold.");
        if (role.Equals("Technical Consultant", StringComparison.OrdinalIgnoreCase))
            gaps.Add("Role needs manual review for exact routing.");
        if (!body.Contains("sla", StringComparison.OrdinalIgnoreCase) &&
            !body.Contains("deadline", StringComparison.OrdinalIgnoreCase))
            gaps.Add("SLA date not clearly mentioned in email.");

        return gaps;
    }

    private static async Task<List<int>> FindRecommendedCandidateIdsAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string role,
        decimal budget,
        List<string> skills)
    {
        const string sql = @"
            select
                candidate_id as id,
                candidate_current_role as current_role,
                expected_budget,
                skills_json::text
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
            if (currentRole.Contains(role, StringComparison.OrdinalIgnoreCase) ||
                role.Contains(currentRole, StringComparison.OrdinalIgnoreCase))
                score += 4;

            if (expectedBudget <= budget)
                score += 3;

            score += candidateSkills.Count(s =>
                skills.Any(x => x.Equals(s, StringComparison.OrdinalIgnoreCase)));

            if (score > 0)
                recommended.Add((id, score));
        }

        await reader.CloseAsync();

        return recommended
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Id)
            .Take(5)
            .Select(x => x.Id)
            .ToList();
    }
}