using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dbiz.MailboxWorker.Models;
using HtmlAgilityPack;

namespace Dbiz.MailboxWorker.Services;

public static class MailExtractionService
{
    private static readonly string[] KnownSkills =
    [
        "Azure", "AWS", "GCP", "Terraform", "CI/CD", "DevOps", "Windows", "Linux", "VMware", "Office 365",
        "Microsoft 365", "Active Directory", "Intune", "SCCM", "PowerShell", "Networking", "Firewall", "Switching",
        "Routing", "Servers", "Storage", "Backup", "Jira", "Asana", "MS Project", "SQL", "TypeScript", "React"
    ];

    public static PlannerMailExtraction ExtractFromMessage(MailMessageSummary message)
    {
        var jdText = BuildBestJdText(message);
        var fullText = string.Join("\n\n", new[] { message.Subject, message.BodyText, jdText }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var result = new PlannerMailExtraction
        {
            MailSubject = message.Subject,
            MailReceivedDateTime = message.ReceivedDateTime,
            IsRead = message.IsRead,
            HasAttachments = message.HasAttachments,
            VendorContactName = message.FromName,
            VendorContactEmail = message.FromAddress,
            RawBodyHtml = message.BodyHtml,
            RawBodyText = message.BodyText,
            JobDescriptionText = jdText,
            JdExpectedFromAttachment = message.HasAttachments,
            SourceType = "Mailbox"
        };

        result.RequestType = Contains(fullText, "CV Request") ? "CV Request" : "Requirement Mail";
        result.RequestId = RegexMatch(fullText, @"\b[A-Z]{2,10}\(?[A-Z]?\)?\d{4,}\b") ?? RegexMatch(fullText, @"\bGVT\(T\)\d+\b");
        result.Category = RegexMatch(fullText, @"Category\s+1A\s+Services\s*\(Individuals Onshore\)") ?? InferCategory(fullText, jdText);
        result.Role = ExtractRole(fullText, jdText);
        result.RequirementTitle = FirstNotBlank(result.Role, result.RequestId, message.Subject);
        result.ClientName = ExtractClientName(fullText);
        result.ClientCluster = RegexMatch(fullText, @"Prime Minister[’']?s Office Cluster");
        result.ClientContactName = ExtractClientContact(fullText);
        result.ClientContactDesignation = ExtractClientDesignation(fullText);
        result.NumberOfPersonnel = ExtractOpenPositions(fullText);
        result.DurationText = RegexMatch(fullText, @"\b\d+\s*-\s*\d+\s*Months\b") ?? RegexMatch(fullText, @"\b\d+\+?\s*Months\b");
        result.SkillLevel = RegexMatch(fullText, @"\bAssociate Consultant\b|\bSenior Consultant\b|\bConsultant\b");
        result.ProjectStartDate = TryExtractProjectDate(fullText, 1);
        result.ProjectEndDate = TryExtractProjectDate(fullText, 2);
        result.SubmissionDeadlineText = RegexMatch(fullText, @"\b\d{2}/\d{2}/\d{4}\s*\([^\)]*working days?[^\)]*\)")
                                      ?? RegexMatch(fullText, @"by\s+\d{2}/\d{2}/\d{4}");
        result.SubmissionDeadline = TryParseDateFromText(result.SubmissionDeadlineText);
        result.SubmissionRequirements = ExtractSubmissionRequirements(fullText);
        result.EvaluationProcess = RegexMatch(fullText, @"Proposed candidates may be required to undergo .*? evaluation process\.?$")
                                  ?? RegexMatch(fullText, @"technical assessments and interviews");
        result.BudgetAmount = TryExtractBudget(fullText, out var budgetText);
        result.BudgetText = budgetText;
        result.BudgetMaxAmount = TryExtractMaxBudget(fullText);
        result.RequirementAsked = BuildRequirementAsked(message.BodyText, jdText);
        result.RequirementSummary = BuildRequirementSummary(result, message.BodyPreview, jdText);
        result.Notes = BuildNotes(result, message);
        result.ExperienceRequired = ExtractExperienceRequired(jdText, fullText);
        result.Location = ExtractLocation(fullText, jdText) ?? "Singapore";
        result.WorkMode = ExtractWorkMode(fullText, jdText);
        result.EmploymentType = ExtractEmploymentType(fullText, jdText);
        result.Priority = InferPriority(result.SubmissionDeadline, result.BudgetAmount);
        result.PrimarySkills = ExtractSkills(fullText, jdText);
        result.SecondarySkills = ExtractSecondarySkills(jdText, result.PrimarySkills);
        result.Gaps = BuildGaps(result);
        result.Timeline = BuildTimeline(result, message);

        return result;
    }

    public static MailMessageSummary? BuildSummaryFromGraphJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("value", out var value) || value.GetArrayLength() == 0)
        {
            return null;
        }

        var msg = value[0];
        var bodyHtml = GetNestedString(msg, "body", "content") ?? string.Empty;
        return new MailMessageSummary
        {
            Id = GetString(msg, "id") ?? Guid.NewGuid().ToString("N"),
            Subject = GetString(msg, "subject") ?? "(no subject)",
            InternetMessageId = GetString(msg, "internetMessageId") ?? string.Empty,
            FromAddress = GetNestedString(msg, "from", "emailAddress", "address") ?? string.Empty,
            FromName = GetNestedString(msg, "from", "emailAddress", "name") ?? string.Empty,
            ReceivedDateTime = TryGetDateTimeOffset(msg, "receivedDateTime"),
            BodyPreview = GetString(msg, "bodyPreview") ?? string.Empty,
            BodyHtml = bodyHtml,
            BodyText = HtmlToText(bodyHtml),
            ConversationId = GetString(msg, "conversationId") ?? string.Empty,
            IsRead = GetBool(msg, "isRead"),
            HasAttachments = GetBool(msg, "hasAttachments"),
            ToRecipients = GetRecipients(msg, "toRecipients"),
            CcRecipients = GetRecipients(msg, "ccRecipients"),
            Attachments = []
        };
    }

    private static string BuildBestJdText(MailMessageSummary message)
    {
        var jdAttachment = message.Attachments
            .Where(x => x.LooksLikeJobDescription && !string.IsNullOrWhiteSpace(x.ExtractedText))
            .OrderByDescending(x => x.ExtractedText.Length)
            .FirstOrDefault();

        if (jdAttachment is not null) return jdAttachment.ExtractedText;

        var anyAttachment = message.Attachments
            .Where(x => !string.IsNullOrWhiteSpace(x.ExtractedText))
            .OrderByDescending(x => x.ExtractedText.Length)
            .FirstOrDefault();

        return anyAttachment?.ExtractedText ?? string.Empty;
    }

    private static string ExtractRole(string fullText, string jdText)
    {
        return RegexMatch(jdText, @"Job Title\s*:\s*(.+)")?.Split('\n')[0].Trim()
               ?? RegexMatch(fullText, @"\bIT Infrastructure Engineer\b")
               ?? RegexMatch(fullText, @"\b[A-Z][A-Za-z/ &\-]+(?:Engineer|Developer|Analyst|Manager|Architect|Administrator|Consultant)\b")
               ?? "General Requirement";
    }

    private static string? ExtractClientName(string text)
    {
        return RegexMatch(text, @"Government Technology Agency\s*\(GovTech Singapore\)")
               ?? RegexMatch(text, @"GovTech Singapore")
               ?? RegexMatch(text, @"Client\s*[:\-]\s*([^\n]+)")?.Split(':', 2).Last().Trim()
               ?? "DBiz Internal";
    }

    private static string? ExtractClientContact(string text)
    {
        return RegexMatch(text, @"Jason CK Lee")
               ?? RegexMatch(text, @"Regards[,]?\s*([A-Z][A-Za-z .]+)")
               ?? RegexMatch(text, @"Thanks[,]?\s*([A-Z][A-Za-z .]+)");
    }

    private static string? ExtractClientDesignation(string text)
        => RegexMatch(text, @"\bSAIS SDL\b")
           ?? RegexMatch(text, @"Designation\s*[:\-]\s*([^\n]+)")?.Split(':', 2).Last().Trim();

    private static string InferCategory(string fullText, string jdText)
    {
        var combined = $"{fullText}\n{jdText}".ToLowerInvariant();
        if (combined.Contains("infra")) return "Infrastructure";
        if (combined.Contains("cloud")) return "Cloud";
        if (combined.Contains("business analyst")) return "Functional";
        return "General";
    }

    private static int ExtractOpenPositions(string text)
    {
        var match = Regex.Match(text, @"No of Personnel\s*\D*(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var count)) return count;

        match = Regex.Match(text, @"Need\s+(\d+)\s+", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out count) ? count : 1;
    }

    private static DateOnly? TryExtractProjectDate(string text, int position)
    {
        var match = Regex.Match(text, @"period of\s+(\d{2}/\d{2}/\d{4})\s+to\s+(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        return ParseDateOnly(match.Groups[position].Value);
    }

    private static DateTimeOffset? TryParseDateFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var dateMatch = Regex.Match(text, @"\d{2}/\d{2}/\d{4}");
        if (!dateMatch.Success) return null;
        if (DateTime.TryParseExact(dateMatch.Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return new DateTimeOffset(dt, TimeSpan.Zero);
        }
        return null;
    }

    private static List<string> ExtractSubmissionRequirements(string text)
    {
        var candidates = new[]
        {
            "Detailed curriculum vitae",
            "Job role and skill level classification",
            "Availability and start date",
            "Proposed pricing (as per Master Contract rates)",
            "Any relevant certifications or qualifications"
        };

        return candidates.Where(x => Contains(text, x)).ToList();
    }

    private static decimal? TryExtractBudget(string text, out string? matchedText)
    {
        var match = Regex.Match(text, @"(?:budget|rate|pricing)\s*[:\-]?\s*(?:SGD|S\$|\$)?\s*(\d{4,6}(?:\.\d{1,2})?)", RegexOptions.IgnoreCase);
        matchedText = match.Success ? match.Value.Trim() : (Contains(text, "Master Contract rates") ? "As per Master Contract rates" : null);
        if (match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
        {
            return amount;
        }
        return null;
    }

    private static decimal? TryExtractMaxBudget(string text)
    {
        var matches = Regex.Matches(text, @"(?:SGD|S\$|\$)\s*(\d{4,6}(?:\.\d{1,2})?)", RegexOptions.IgnoreCase);
        decimal? max = null;
        foreach (Match m in matches)
        {
            if (decimal.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            {
                max = max.HasValue ? Math.Max(max.Value, amount) : amount;
            }
        }
        return max;
    }

    private static string BuildRequirementAsked(string bodyText, string jdText)
    {
        var source = !string.IsNullOrWhiteSpace(jdText) ? jdText : bodyText;
        return Truncate(source, 5000);
    }

    private static string BuildRequirementSummary(PlannerMailExtraction extraction, string bodyPreview, string jdText)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(extraction.Role)) parts.Add(extraction.Role!);
        if (!string.IsNullOrWhiteSpace(extraction.ClientName)) parts.Add($"for {extraction.ClientName}");
        if (!string.IsNullOrWhiteSpace(extraction.DurationText)) parts.Add($"duration {extraction.DurationText}");
        if (!string.IsNullOrWhiteSpace(extraction.SkillLevel)) parts.Add($"level {extraction.SkillLevel}");
        if (extraction.NumberOfPersonnel.HasValue) parts.Add($"positions {extraction.NumberOfPersonnel}");
        if (extraction.PrimarySkills.Count > 0) parts.Add($"skills {string.Join(", ", extraction.PrimarySkills.Take(5))}");

        var summary = string.Join("; ", parts);
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = !string.IsNullOrWhiteSpace(bodyPreview) ? bodyPreview : Truncate(jdText, 300);
        }
        return Truncate(summary, 2000);
    }

    private static string BuildNotes(PlannerMailExtraction extraction, MailMessageSummary message)
    {
        var notes = new List<string>();
        if (extraction.BudgetAmount is null) notes.Add("Budget not explicitly stated in email; pricing requested as per contract if applicable.");
        if (string.Equals(extraction.ClientName, "DBiz Internal", StringComparison.OrdinalIgnoreCase)) notes.Add("Client defaulted from available mail content.");
        if (message.HasAttachments && string.IsNullOrWhiteSpace(extraction.JobDescriptionText)) notes.Add("Attachment exists but no text could be extracted.");
        return string.Join(" ", notes);
    }

    private static string? ExtractExperienceRequired(string jdText, string fullText)
        => RegexMatch(jdText, @"\b\d+\+?\s+years?\b")
           ?? RegexMatch(fullText, @"\b\d+\+?\s+years?\b");

    private static string? ExtractLocation(string fullText, string jdText)
    {
        var combined = $"{fullText}\n{jdText}";
        return RegexMatch(combined, @"\bSingapore\b")
               ?? RegexMatch(combined, @"Location\s*[:\-]\s*([^\n]+)")?.Split(':', 2).Last().Trim();
    }

    private static string ExtractWorkMode(string fullText, string jdText)
    {
        var combined = $"{fullText}\n{jdText}".ToLowerInvariant();
        if (combined.Contains("hybrid")) return "Hybrid";
        if (combined.Contains("onsite") || combined.Contains("on-site") || combined.Contains("onshore")) return "Onsite";
        if (combined.Contains("remote")) return "Remote";
        return string.Empty;
    }

    private static string ExtractEmploymentType(string fullText, string jdText)
    {
        var combined = $"{fullText}\n{jdText}".ToLowerInvariant();
        if (combined.Contains("contract")) return "Contract";
        if (combined.Contains("permanent")) return "Permanent";
        return string.Empty;
    }

    private static string InferPriority(DateTimeOffset? submissionDeadline, decimal? budget)
    {
        if (submissionDeadline.HasValue && submissionDeadline.Value <= DateTimeOffset.UtcNow.AddDays(2)) return "High";
        if (budget.HasValue && budget.Value >= 8000) return "High";
        if (submissionDeadline.HasValue && submissionDeadline.Value <= DateTimeOffset.UtcNow.AddDays(5)) return "Medium";
        return "Medium";
    }

    private static List<string> ExtractSkills(string fullText, string jdText)
    {
        var combined = $"{fullText}\n{jdText}";
        return KnownSkills.Where(skill => Regex.IsMatch(combined, $@"(?<![A-Za-z0-9]){Regex.Escape(skill)}(?![A-Za-z0-9])", RegexOptions.IgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }

    private static List<string> ExtractSecondarySkills(string jdText, List<string> primarySkills)
    {
        var secondary = new[] { "Risk Management", "Budgeting", "Presentation Skills", "Change Management", "Stakeholder Management" }
            .Where(x => Contains(jdText, x) && !primarySkills.Contains(x, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return secondary;
    }

    private static List<string> BuildGaps(PlannerMailExtraction extraction)
    {
        var gaps = new List<string>();
        if (extraction.SubmissionDeadline is null) gaps.Add("SLA date not clearly mentioned. Default SLA logic applied.");
        if (extraction.BudgetAmount is null) gaps.Add("Budget not found in mail/JD.");
        if (string.IsNullOrWhiteSpace(extraction.ClientContactName)) gaps.Add("Client contact name not clearly identified.");
        return gaps;
    }

    private static List<PlannerTimelineItem> BuildTimeline(PlannerMailExtraction extraction, MailMessageSummary message)
    {
        var items = new List<PlannerTimelineItem>
        {
            new()
            {
                Title = "Mail received",
                HappenedOn = message.ReceivedDateTime,
                Description = "Requirement email captured by mailbox worker.",
                PerformedBy = string.IsNullOrWhiteSpace(message.FromName) ? "Mailbox" : message.FromName
            },
            new()
            {
                Title = "Task created",
                HappenedOn = DateTimeOffset.UtcNow,
                Description = "Planner task created from mailbox extraction.",
                PerformedBy = "Mailbox Worker"
            }
        };

        if (extraction.SubmissionDeadline.HasValue)
        {
            items.Add(new PlannerTimelineItem
            {
                Title = "Submission deadline",
                HappenedOn = extraction.SubmissionDeadline,
                Description = extraction.SubmissionDeadlineText ?? "Submission due date extracted from mail.",
                PerformedBy = extraction.ClientName ?? "Client"
            });
        }

        return items;
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static bool Contains(string text, string value)
        => text?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;

    private static string? RegexMatch(string input, string pattern)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Multiline);
        if (!match.Success) return null;
        return match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : match.Value.Trim();
    }

    private static DateOnly? ParseDateOnly(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        return DateOnly.TryParseExact(input, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static string HtmlToText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var text = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string? GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;

    private static bool GetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop)) return false;
        return prop.ValueKind == JsonValueKind.True || (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out var v) && v);
    }

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop)) return null;
        return DateTimeOffset.TryParse(prop.GetString(), out var dt) ? dt : null;
    }

    private static string? GetNestedString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current)) return null;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }

    private static List<string> GetRecipients(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array) return [];
        var items = new List<string>();
        foreach (var entry in prop.EnumerateArray())
        {
            var address = GetNestedString(entry, "emailAddress", "address");
            if (!string.IsNullOrWhiteSpace(address)) items.Add(address);
        }
        return items;
    }

    private static string FirstNotBlank(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}
