namespace PlannerApi.Dtos.Auth;

public class LoginResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? Token { get; set; }
    public string? RoleCode { get; set; }
    public int? UserId { get; set; }
    public int? VendorId { get; set; }
    public bool RequiresFirstLogin { get; set; }
}
