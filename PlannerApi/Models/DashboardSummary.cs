namespace PlannerApi.Models;

public class DashboardSummary
{
    public int NewTasks { get; set; }
    public int UnderReview { get; set; }
    public int AssignedToVendors { get; set; }
    public int ClosingToday { get; set; }
}
