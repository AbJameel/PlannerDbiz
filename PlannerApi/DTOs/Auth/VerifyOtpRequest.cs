namespace PlannerApi.Dtos.Auth;

public class VerifyOtpRequest
{
    public string Email { get; set; } = "";
    public Guid ActivationToken { get; set; }
    public string OtpCode { get; set; } = "";
}
