namespace Dbiz.MailboxWorker.Models;

public sealed class CreatePlannerFromMailRequest
{
    public string Source { get; set; } = "mailbox-worker";
    public string MessageId { get; set; } = string.Empty;
    public string InternetMessageId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public DateTimeOffset? ReceivedDateTime { get; set; }
    public string BodyPreview { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string JobDescriptionText { get; set; } = string.Empty;
    public bool HasAttachments { get; set; }
    public List<string> ToRecipients { get; set; } = [];
    public List<string> CcRecipients { get; set; } = [];
    public List<CreatePlannerAttachmentDto> Attachments { get; set; } = [];
    public PlannerMailExtraction Extraction { get; set; } = new();
}

public sealed class CreatePlannerAttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int Size { get; set; }
    public string SavedFilePath { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public bool LooksLikeJobDescription { get; set; }
}
