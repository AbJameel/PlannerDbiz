namespace PlannerApi.Models;

public class TaskTimelineItem
{
    public DateTime HappenedOn { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
}
