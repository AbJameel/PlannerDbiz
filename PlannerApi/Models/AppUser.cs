namespace PlannerApi.Models;

public class AppUser
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string RoleCode { get; set; } = "";
    public int? VendorId { get; set; }
    public bool IsActive { get; set; }
    public bool IsFirstLogin { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? UpdatedOn { get; set; }
}
