namespace Dbiz.MailboxWorker.Options;

public sealed class GraphOptions
{
    public const string SectionName = "Graph";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string MailboxAddress { get; set; } = string.Empty;
    public string MailFolderId { get; set; } = "Inbox";
    public string[] Scopes { get; set; } = ["https://graph.microsoft.com/.default"];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TenantId))
            throw new InvalidOperationException("Graph:TenantId is required.");

        if (string.IsNullOrWhiteSpace(ClientId))
            throw new InvalidOperationException("Graph:ClientId is required.");

        if (string.IsNullOrWhiteSpace(ClientSecret))
            throw new InvalidOperationException("Graph:ClientSecret is required.");

        if (string.IsNullOrWhiteSpace(MailboxAddress))
            throw new InvalidOperationException("Graph:MailboxAddress is required.");
    }
}
