using Dbiz.MailboxWorker;
using Dbiz.MailboxWorker.Options;
using Dbiz.MailboxWorker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "DBiz Mailbox Worker";
});

builder.Services.Configure<GraphOptions>(builder.Configuration.GetSection(GraphOptions.SectionName));
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));
builder.Services.Configure<PlannerApiOptions>(builder.Configuration.GetSection(PlannerApiOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services.AddSingleton<IGraphClientFactory, DbizGraphClientFactory>();
builder.Services.AddSingleton<ProcessedStateStore>();
builder.Services.AddSingleton<IAttachmentTextExtractor, AttachmentTextExtractor>();
builder.Services.AddSingleton<IPlannerClient, PlannerClient>();
builder.Services.AddSingleton<IMailboxReaderService, MailboxReaderService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
