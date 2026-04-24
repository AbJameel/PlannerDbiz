namespace Dbiz.MailboxWorker.Models;

public sealed class MailMessageSummary
{
    public required string Id { get; init; }
    public required string Subject { get; init; }
    public required string InternetMessageId { get; init; }
    public required string FromAddress { get; init; }
    public required string FromName { get; init; }
    public required DateTimeOffset? ReceivedDateTime { get; init; }
    public required string BodyPreview { get; init; }
    public required string BodyHtml { get; init; }
    public required string BodyText { get; init; }
    public required string ConversationId { get; init; }
    public required bool IsRead { get; init; }
    public required bool HasAttachments { get; init; }
    public List<string> ToRecipients { get; init; } = [];
    public List<string> CcRecipients { get; init; } = [];
    public List<MailAttachmentContent> Attachments { get; init; } = [];
}
