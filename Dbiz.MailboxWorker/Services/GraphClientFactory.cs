using Azure.Identity;
using Dbiz.MailboxWorker.Options;
using Microsoft.Extensions.Options;

namespace Dbiz.MailboxWorker.Services;

public sealed class DbizGraphClientFactory : IGraphClientFactory
{
    private readonly GraphOptions _options;

    public DbizGraphClientFactory(IOptions<GraphOptions> options)
    {
        _options = options.Value;
    }

    public Microsoft.Graph.GraphServiceClient CreateClient()
    {
        _options.ValidateWhenEnabled();

        var credential = new ClientSecretCredential(
            _options.TenantId,
            _options.ClientId,
            _options.ClientSecret);

        return new Microsoft.Graph.GraphServiceClient(credential, _options.Scopes);
    }
}
