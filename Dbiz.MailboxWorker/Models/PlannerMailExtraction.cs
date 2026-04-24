namespace Dbiz.MailboxWorker.Models;

public sealed class PlannerMailExtraction
{
    public string? MailSubject { get; set; }
    public DateTimeOffset? MailReceivedDateTime { get; set; }
    public bool IsRead { get; set; }
    public bool HasAttachments { get; set; }

    public string? RequestId { get; set; }
    public string? RequestType { get; set; }

    public string? ClientName { get; set; }
    public string? ClientCluster { get; set; }
    public string? ClientContactName { get; set; }
    public string? ClientContactDesignation { get; set; }

    public string? VendorContactName { get; set; }
    public string? VendorContactEmail { get; set; }

    public string? Category { get; set; }
    public string? Role { get; set; }
    public string? DurationText { get; set; }
    public string? SkillLevel { get; set; }
    public int? NumberOfPersonnel { get; set; }

    public DateOnly? ProjectStartDate { get; set; }
    public DateOnly? ProjectEndDate { get; set; }
    public DateTimeOffset? SubmissionDeadline { get; set; }
    public string? SubmissionDeadlineText { get; set; }
    public List<string> SubmissionRequirements { get; set; } = [];

    public string? EvaluationProcess { get; set; }

    public decimal? BudgetAmount { get; set; }
    public decimal? BudgetMaxAmount { get; set; }
    public string? BudgetText { get; set; }

    public string? JobDescriptionText { get; set; }
    public bool JdExpectedFromAttachment { get; set; }

    public List<string> PrimarySkills { get; set; } = [];
    public List<string> SecondarySkills { get; set; } = [];
    public List<string> Gaps { get; set; } = [];
    public List<PlannerTimelineItem> Timeline { get; set; } = [];

    public string? RequirementTitle { get; set; }
    public string? RequirementSummary { get; set; }
    public string? RequirementAsked { get; set; }
    public string? Notes { get; set; }
    public string? ExperienceRequired { get; set; }
    public string? Location { get; set; }
    public string? WorkMode { get; set; }
    public string? EmploymentType { get; set; }
    public string? Priority { get; set; }
    public string? SourceType { get; set; }

    public string? RawBodyHtml { get; set; }
    public string? RawBodyText { get; set; }
}

public sealed class PlannerTimelineItem
{
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset? HappenedOn { get; set; }
    public string Description { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
}
