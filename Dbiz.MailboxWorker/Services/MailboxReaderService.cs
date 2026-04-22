using Dbiz.MailboxWorker.Models;
using Dbiz.MailboxWorker.Options;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Dbiz.MailboxWorker.Services;

public sealed class MailboxReaderService : IMailboxReaderService
{
    private readonly ILogger<MailboxReaderService> _logger;
    private readonly IGraphClientFactory _graphClientFactory;
    private readonly ProcessedStateStore _processedStateStore;
    private readonly GraphOptions _graphOptions;
    private readonly WorkerOptions _workerOptions;

    public MailboxReaderService(
        ILogger<MailboxReaderService> logger,
        IGraphClientFactory graphClientFactory,
        ProcessedStateStore processedStateStore,
        IOptions<GraphOptions> graphOptions,
        IOptions<WorkerOptions> workerOptions)
    {
        _logger = logger;
        _graphClientFactory = graphClientFactory;
        _processedStateStore = processedStateStore;
        _graphOptions = graphOptions.Value;
        _workerOptions = workerOptions.Value;
    }

    public async Task ProcessInboxAsync(CancellationToken cancellationToken)
    {
        var graphClient = _graphClientFactory.CreateClient();

        var response = await graphClient
            .Users[_graphOptions.MailboxAddress]
            .MailFolders[_graphOptions.MailFolderId]
            .Messages
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Top = _workerOptions.MaxMessagesPerPoll;
                requestConfiguration.QueryParameters.Select = [
                    "id",
                    "subject",
                    "from",
                    "receivedDateTime",
                    "bodyPreview",
                    "conversationId",
                    "isRead"
                ];
                requestConfiguration.QueryParameters.Orderby = ["receivedDateTime desc"];

                if (_workerOptions.OnlyUnread)
                {
                    requestConfiguration.QueryParameters.Filter = "isRead eq false";
                }
            }, cancellationToken);

        var messages = response?.Value ?? [];
        if (messages.Count == 0)
        {
            _logger.LogInformation("No matching messages found in mailbox {MailboxAddress}.", _graphOptions.MailboxAddress);
            return;
        }

        _logger.LogInformation("Fetched {Count} message(s) from mailbox {MailboxAddress}.", messages.Count, _graphOptions.MailboxAddress);

        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(message.Id))
            {
                continue;
            }

            if (await _processedStateStore.ExistsAsync(message.Id, cancellationToken))
            {
                _logger.LogDebug("Skipping previously processed message: {MessageId}", message.Id);
                continue;
            }

            var summary = ToSummary(message);
            await HandleMessageAsync(summary, cancellationToken);

            if (_workerOptions.MarkAsReadAfterProcessing && message.IsRead != true)
            {
                await MarkAsReadAsync(graphClient, message.Id, cancellationToken);
            }

            await _processedStateStore.AddAsync(message.Id, cancellationToken);
        }
    }

    private async Task HandleMessageAsync(MailMessageSummary message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "New message | Subject: {Subject} | From: {From} | Received: {Received} | ConversationId: {ConversationId}",
            message.Subject,
            message.FromAddress,
            message.ReceivedDateTime,
            message.ConversationId);

        _logger.LogInformation("Body preview: {BodyPreview}", message.BodyPreview);

        // TODO:
        // 1. Save raw email into your database
        // 2. Call JD / requirement extraction service
        // 3. Create planner/task record
        // 4. Apply business rules and vendor assignment
        await Task.CompletedTask;
    }

    private async Task MarkAsReadAsync(GraphServiceClient graphClient, string messageId, CancellationToken cancellationToken)
    {
        var patchBody = new Message
        {
            IsRead = true
        };

        await graphClient
            .Users[_graphOptions.MailboxAddress]
            .Messages[messageId]
            .PatchAsync(patchBody, cancellationToken: cancellationToken);

        _logger.LogInformation("Marked message as read: {MessageId}", messageId);
    }

    private static MailMessageSummary ToSummary(Message message)
    {
        return new MailMessageSummary
        {
            Id = message.Id ?? string.Empty,
            Subject = message.Subject ?? "(no subject)",
            FromAddress = message.From?.EmailAddress?.Address ?? "(unknown)",
            ReceivedDateTime = message.ReceivedDateTime,
            BodyPreview = message.BodyPreview ?? string.Empty,
            ConversationId = message.ConversationId ?? string.Empty,
            IsRead = message.IsRead ?? false
        };
    }
}
