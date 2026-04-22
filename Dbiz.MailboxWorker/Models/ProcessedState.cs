namespace Dbiz.MailboxWorker.Models;

public sealed class ProcessedState
{
    public HashSet<string> MessageIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
