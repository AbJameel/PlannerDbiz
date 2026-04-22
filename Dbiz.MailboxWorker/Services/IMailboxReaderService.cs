namespace Dbiz.MailboxWorker.Services;

public interface IMailboxReaderService
{
    Task ProcessInboxAsync(CancellationToken cancellationToken);
}
