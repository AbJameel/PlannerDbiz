namespace Dbiz.MailboxWorker.Models;

public sealed class MailAttachmentContent
{
    public string AttachmentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int Size { get; set; }
    public bool IsInline { get; set; }
    public string SavedFilePath { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public string ExtractionStatus { get; set; } = string.Empty;
    public bool LooksLikeJobDescription { get; set; }
}
