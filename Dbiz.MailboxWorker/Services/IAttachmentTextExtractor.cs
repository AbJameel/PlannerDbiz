namespace Dbiz.MailboxWorker.Services;

public interface IAttachmentTextExtractor
{
    Task<(string Text, string Status)> ExtractTextAsync(string filePath, CancellationToken cancellationToken);
}
