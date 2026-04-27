namespace PlannerApi.Models;

public class DashboardSummary
{
    public int NewTasks { get; set; }
    public int UnderReview { get; set; }
    public int AssignedToVendors { get; set; }
    public int ClosingToday { get; set; }

    // Vendor dashboard values. These are also returned for admins as 0.
    public int AssignedQueue { get; set; }
    public int PendingSubmission { get; set; }
    public int RepliedToRecruiter { get; set; }
    public int SlaToday { get; set; }
}
