namespace PlannerApi.Models;

public class UserActivation
{
    public int ActivationId { get; set; }
    public int UserId { get; set; }
    public Guid ActivationToken { get; set; }
    public string OtpCode { get; set; } = "";
    public DateTime OtpExpiry { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedOn { get; set; }
}
