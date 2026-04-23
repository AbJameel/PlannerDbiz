namespace PlannerApi.Services;

public interface IEmailService
{
    Task SendUserActivationAsync(string toEmail, string fullName, Guid activationToken, string otpCode, string roleCode);
}
