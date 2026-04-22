using Microsoft.Graph;

namespace Dbiz.MailboxWorker.Services;

public sealed class GraphClientFactory : IGraphClientFactory
{
    private readonly GraphServiceClient _graphServiceClient;

    public GraphClientFactory(GraphServiceClient graphServiceClient)
    {
        _graphServiceClient = graphServiceClient;
    }

    public GraphServiceClient CreateClient() => _graphServiceClient;
}
