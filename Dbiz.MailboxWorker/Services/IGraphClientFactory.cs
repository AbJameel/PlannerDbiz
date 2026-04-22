using Microsoft.Graph;

namespace Dbiz.MailboxWorker.Services;

public interface IGraphClientFactory
{
    GraphServiceClient CreateClient();
}
