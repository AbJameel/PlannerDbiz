namespace PlannerApi.Models;

public class PlannerContact
{
    public int Id { get; set; }
    public int PlannerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Agency { get; set; } = string.Empty;
    public string ContactLevel { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
