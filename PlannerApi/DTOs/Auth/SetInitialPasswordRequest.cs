namespace PlannerApi.Dtos.Auth;

public class SetInitialPasswordRequest
{
    public string Email { get; set; } = "";
    public Guid ActivationToken { get; set; }
    public string OtpCode { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}
