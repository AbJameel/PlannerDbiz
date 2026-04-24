namespace Dbiz.MailboxWorker.Options;

public sealed class PlannerApiOptions
{
    public const string SectionName = "PlannerApi";

    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = string.Empty;
    public string CreatePlannerPath { get; set; } = "/api/planner/mail/create";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyHeaderName { get; set; } = "x-api-key";
    public string BearerToken { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 100;

    public void ValidateWhenEnabled()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException("PlannerApi:BaseUrl is required when PlannerApi:Enabled is true.");

        if (TimeoutSeconds <= 0)
            throw new InvalidOperationException("PlannerApi:TimeoutSeconds must be greater than 0.");
    }
}
