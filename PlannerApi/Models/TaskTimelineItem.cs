namespace PlannerApi.Models;

public class TaskTimelineItem
{
    public DateTime HappenedOn { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
}
public class PlannerListItem
{
    public int Id { get; set; }
    public string PlannerNo { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string RequirementTitle { get; set; } = "";
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public decimal Budget { get; set; }
    public string Currency { get; set; } = "";
    public DateTime SlaDate { get; set; }
    public int OpenPositions { get; set; }
    public int PlannerId { get; internal set; }
    public string Title { get; internal set; }
    public DateTime CreatedAt { get; internal set; }
}