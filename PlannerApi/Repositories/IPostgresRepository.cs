using PlannerApi.Models;

namespace PlannerApi.Repositories;

public interface IPostgresRepository
{
    Task<IReadOnlyList<PlannerTask>> GetTasksAsync();
    Task<PlannerTask?> GetTaskAsync(int id);
    Task<IReadOnlyList<Candidate>> GetCandidatesAsync();
    Task<IReadOnlyList<Rule>> GetRulesAsync();
    Task<IReadOnlyList<Vendor>> GetVendorsAsync();
    Task<IReadOnlyList<MailboxItem>> GetMailboxAsync();
    Task<DashboardSummary> GetSummaryAsync();
    Task<IReadOnlyList<Candidate>> GetRecommendedCandidatesAsync(int taskId);
    Task<int> CreateTaskAsync(string subject, string fromEmail, string body, string? sourceType = null);
    Task<int> CreateRuleAsync(Rule rule);
    Task<int> CreateVendorAsync(Vendor vendor);
    Task<int> CreateCandidateAsync(Candidate candidate);
}
