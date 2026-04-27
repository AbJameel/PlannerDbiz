using PlannerApi.Models;

namespace PlannerApi.DTOs;

public class UpdateTaskRequest
{
    public string ClientName { get; set; } = "DBiz Internal";
    public string RequirementTitle { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? SeniorityLevel { get; set; }
    public string Category { get; set; } = "General";
    public decimal Budget { get; set; }
    public decimal? BudgetMax { get; set; }
    public string Currency { get; set; } = "SGD";
    public DateTime SlaDate { get; set; }
    public int OpenPositions { get; set; } = 1;
    public string Priority { get; set; } = "Medium";
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public List<PlannerContact>? Contacts { get; set; }
    public string RequirementAsked { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = [];
    public List<string> SecondarySkills { get; set; } = [];
    public string ExperienceRequired { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string WorkMode { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public string Status { get; set; } = "In Review";
    public string RecruiterOverrideComment { get; set; } = string.Empty;
}
