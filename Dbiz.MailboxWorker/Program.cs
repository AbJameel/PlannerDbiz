using Dbiz.MailboxWorker.Options;
using Dbiz.MailboxWorker.Services;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Azure.Identity;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "DBiz Mailbox Worker";
});

builder.Services.Configure<GraphOptions>(builder.Configuration.GetSection(GraphOptions.SectionName));
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));

builder.Services.AddSingleton<IGraphClientFactory, Dbiz.MailboxWorker.Services.GraphClientFactory>();
builder.Services.AddSingleton<ProcessedStateStore>();
builder.Services.AddSingleton<IMailboxReaderService, MailboxReaderService>();
builder.Services.AddHostedService<Dbiz.MailboxWorker.Worker>();

builder.Services.AddSingleton(sp =>
{
    var graphOptions = sp.GetRequiredService<IOptions<GraphOptions>>().Value;
    graphOptions.Validate();

    var credential = new ClientSecretCredential(
        graphOptions.TenantId,
        graphOptions.ClientId,
        graphOptions.ClientSecret);

    return credential;
});

builder.Services.AddSingleton(sp =>
{
    var credential = sp.GetRequiredService<ClientSecretCredential>();
    var graphOptions = sp.GetRequiredService<IOptions<GraphOptions>>().Value;

    return new GraphServiceClient(credential, graphOptions.Scopes);
});

var host = builder.Build();
host.Run();
