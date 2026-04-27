namespace PlannerApi.Models;

public class PlannerTask
{
    public int Id { get; set; }
    public string PlannerNo { get; set; } = string.Empty;
    public string ClientName { get; set; } = "DBiz Internal";
    public string RequirementTitle { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string SeniorityLevel { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Priority { get; set; } = "Medium";
    public decimal Budget { get; set; }
    public decimal? BudgetMax { get; set; }
    public string Currency { get; set; } = "SGD";
    public DateTime ReceivedOn { get; set; }
    public DateTime SlaDate { get; set; }
    public string Status { get; set; } = "New";
    public int OpenPositions { get; set; } = 1;
    public string SourceType { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public List<PlannerContact> Contacts { get; set; } = [];
    public string RequirementAsked { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string VendorComment { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = [];
    public List<string> SecondarySkills { get; set; } = [];
    public List<string> Gaps { get; set; } = [];
    public string ExperienceRequired { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string WorkMode { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public string RecruiterOverrideComment { get; set; } = string.Empty;
    public List<TaskTimelineItem> Timeline { get; set; } = [];
    public List<int> RecommendedCandidateIds { get; set; } = [];
    public List<int> AssignedVendorIds { get; set; } = [];
}
