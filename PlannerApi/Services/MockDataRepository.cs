using System.Text.Json;
using PlannerApi.Models;

namespace PlannerApi.Services;

public class MockDataRepository
{
    private readonly IWebHostEnvironment _environment;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public MockDataRepository(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public IReadOnlyList<PlannerTask> GetTasks() => Read<List<PlannerTask>>("tasks.json") ?? [];
    public PlannerTask? GetTask(int id) => GetTasks().FirstOrDefault(x => x.Id == id);
    public IReadOnlyList<Rule> GetRules() => Read<List<Rule>>("rules.json") ?? [];
    public IReadOnlyList<Vendor> GetVendors() => Read<List<Vendor>>("vendors.json") ?? [];
    public IReadOnlyList<Candidate> GetCandidates() => Read<List<Candidate>>("candidates.json") ?? [];
    public IReadOnlyList<MailboxItem> GetMailbox() => Read<List<MailboxItem>>("mailbox.json") ?? [];

    public DashboardSummary GetSummary()
    {
        var tasks = GetTasks();
        return new DashboardSummary
        {
            NewTasks = tasks.Count(x => x.Status.Equals("New", StringComparison.OrdinalIgnoreCase)),
            UnderReview = tasks.Count(x => x.Status.Equals("Under Review", StringComparison.OrdinalIgnoreCase)),
            AssignedToVendors = tasks.Count(x => x.Status.Contains("Assigned", StringComparison.OrdinalIgnoreCase)),
            ClosingToday = tasks.Count(x => x.SlaDate.Date == DateTime.Today)
        };
    }

    private T? Read<T>(string fileName)
    {
        var path = Path.Combine(_environment.ContentRootPath, "Data", fileName);
        if (!File.Exists(path)) return default;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }
}
