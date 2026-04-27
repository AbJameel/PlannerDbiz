using PlannerApi.DTOs;
using PlannerApi.Models;

namespace PlannerApi.Repositories;

public interface IPostgresRepository
{
    Task<IReadOnlyList<PlannerTask>> GetTasksAsync();
    Task<IReadOnlyList<PlannerTask>> GetReviewQueueAsync();
    Task<PlannerTask?> GetTaskAsync(int id);
    Task<IReadOnlyList<Candidate>> GetCandidatesAsync();
    Task<IReadOnlyList<Rule>> GetRulesAsync();
    Task<IReadOnlyList<Vendor>> GetVendorsAsync();
    Task<IReadOnlyList<Vendor>> GetRecommendedVendorsAsync(int taskId);
    Task<IReadOnlyList<MailboxItem>> GetMailboxAsync();
    Task<IReadOnlyList<PlannerContact>> GetContactsAsync();
    Task SaveContactsAsync(IReadOnlyList<PlannerContact> contacts, string performedBy);
    Task<DashboardSummary> GetSummaryAsync();
    Task<DashboardSummary> GetVendorSummaryAsync(int vendorId);
    Task<IReadOnlyList<PlannerTask>> GetTasksForVendorAsync(int vendorId);
    Task<IReadOnlyList<VendorCandidateSubmission>> GetSubmittedCandidatesForVendorAsync(int vendorId);
    Task<IReadOnlyList<Candidate>> GetRecommendedCandidatesAsync(int taskId);
    Task<int> CreateTaskAsync(string subject, string fromEmail, string body, string? sourceType = null, string? fileName = null);
    Task UpdateTaskAsync(int id, UpdateTaskRequest request, string performedBy);
    Task AssignVendorsAsync(int id, AssignVendorsRequest request, string performedBy);
    Task<int> CreateRuleAsync(Rule rule);
    Task<int> CreateVendorAsync(Vendor vendor);
    Task<int> CreateCandidateAsync(Candidate candidate);
    Task<(string VendorComment, string AssignmentNote, IReadOnlyList<VendorCandidateSubmission> Items)> GetVendorSubmissionsAsync(int taskId, int? vendorId = null);
    Task SaveVendorSubmissionsAsync(int taskId, int vendorId, SaveVendorCandidatesRequest request, string performedBy);

    Task<(IReadOnlyList<PlannerListItem> Items, int TotalCount)> GetPlannerListAsync(
        string? status,
        string? priority,
        string? search,
        string? role,
        string? clientName,
        bool? closingToday,
        string? userRole,
        int? vendorId,
        string? slaDate
    );
}

