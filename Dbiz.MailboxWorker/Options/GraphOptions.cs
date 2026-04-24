namespace Dbiz.MailboxWorker.Options;

public sealed class GraphOptions
{
    public const string SectionName = "Graph";

    public bool Enabled { get; set; } = true;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string MailboxAddress { get; set; } = string.Empty;
    public string MailFolderId { get; set; } = "Inbox";
    public string[] Scopes { get; set; } = ["https://graph.microsoft.com/.default"];

    public void ValidateWhenEnabled()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(TenantId))
            throw new InvalidOperationException("Graph:TenantId is required when Graph is enabled.");

        if (string.IsNullOrWhiteSpace(ClientId))
            throw new InvalidOperationException("Graph:ClientId is required when Graph is enabled.");

        if (string.IsNullOrWhiteSpace(ClientSecret))
            throw new InvalidOperationException("Graph:ClientSecret is required when Graph is enabled.");

        if (string.IsNullOrWhiteSpace(MailboxAddress))
            throw new InvalidOperationException("Graph:MailboxAddress is required when Graph is enabled.");
    }
}
