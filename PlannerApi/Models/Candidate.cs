namespace PlannerApi.Models;

public class Candidate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CurrentRole { get; set; } = string.Empty;
    public decimal ExpectedBudget { get; set; }
    public int ExperienceYears { get; set; }
    public string NoticePeriod { get; set; } = string.Empty;
    public string ResumeFile { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = [];
    public string Location { get; set; } = string.Empty;
}
