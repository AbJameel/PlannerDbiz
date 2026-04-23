namespace PlannerApi.Dtos.Users;

public class CreateUserRequest
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string RoleCode { get; set; } = "";
    public int? VendorId { get; set; }
    public bool IsActive { get; set; } = true;
}
