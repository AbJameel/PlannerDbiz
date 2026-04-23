namespace PlannerApi.Services;

public class EmailService(IConfiguration configuration, ILogger<EmailService> logger) : IEmailService
{
    public Task SendUserActivationAsync(string toEmail, string fullName, Guid activationToken, string otpCode, string roleCode)
    {
        var baseUrl = configuration["Email:BaseActivationUrl"] ?? "http://localhost:5173/activate-account";
        var link = $"{baseUrl}?token={activationToken}&email={Uri.EscapeDataString(toEmail)}";

        logger.LogInformation(
            "Activation email simulated. To: {ToEmail}, Name: {FullName}, Role: {RoleCode}, OTP: {OtpCode}, Link: {Link}",
            toEmail, fullName, roleCode, otpCode, link);

        return Task.CompletedTask;
    }
}
