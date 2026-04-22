namespace PlannerApi.Models;

public class MailboxItem
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public DateTime ReceivedOn { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public string SourceType { get; set; } = string.Empty;
}
