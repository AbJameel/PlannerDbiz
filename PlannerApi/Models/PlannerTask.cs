namespace PlannerApi.Models;

public class PlannerTask
{
    public int Id { get; set; }
    public string PlannerNo { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public string Currency { get; set; } = "SGD";
    public DateTime ReceivedOn { get; set; }
    public DateTime SlaDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int OpenPositions { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string RequirementAsked { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = [];
    public List<string> Gaps { get; set; } = [];
    public List<TaskTimelineItem> Timeline { get; set; } = [];
    public List<int> RecommendedCandidateIds { get; set; } = [];
    public List<int> AssignedVendorIds { get; set; } = [];
}
