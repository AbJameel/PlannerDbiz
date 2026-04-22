using Dbiz.MailboxWorker.Options;
using Dbiz.MailboxWorker.Services;
using Microsoft.Extensions.Options;

namespace Dbiz.MailboxWorker;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IMailboxReaderService _mailboxReaderService;
    private readonly WorkerOptions _workerOptions;

    public Worker(
        ILogger<Worker> logger,
        IMailboxReaderService mailboxReaderService,
        IOptions<WorkerOptions> workerOptions)
    {
        _logger = logger;
        _mailboxReaderService = mailboxReaderService;
        _workerOptions = workerOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _workerOptions.Validate();

        _logger.LogInformation("DBiz mailbox worker started. Poll interval: {PollIntervalSeconds}s", _workerOptions.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _mailboxReaderService.ProcessInboxAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while processing the mailbox.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_workerOptions.PollIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("DBiz mailbox worker is stopping.");
    }
}
