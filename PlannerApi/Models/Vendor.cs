namespace PlannerApi.Models;

public class Vendor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UenNo { get; set; } = string.Empty;
    public string PocName { get; set; } = string.Empty;
    public string PocEmail { get; set; } = string.Empty;
    public string PocPhone { get; set; } = string.Empty;
    public string SourcingLocation { get; set; } = string.Empty;
    public string ServingLocation { get; set; } = string.Empty;
    public string CoverageRoles { get; set; } = string.Empty;
    public decimal BudgetMin { get; set; }
    public decimal BudgetMax { get; set; }
    public string Status { get; set; } = string.Empty;
}
