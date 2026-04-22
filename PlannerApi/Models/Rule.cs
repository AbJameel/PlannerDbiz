namespace PlannerApi.Models;

public class Rule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
