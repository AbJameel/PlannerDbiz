using System.Text.RegularExpressions;
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
    private readonly IAttachmentTextExtractor _attachmentTextExtractor;
    private readonly IPlannerClient _plannerClient;
    private readonly GraphOptions _graphOptions;
    private readonly WorkerOptions _workerOptions;

    public MailboxReaderService(
        ILogger<MailboxReaderService> logger,
        IGraphClientFactory graphClientFactory,
        ProcessedStateStore processedStateStore,
        IAttachmentTextExtractor attachmentTextExtractor,
        IPlannerClient plannerClient,
        IOptions<GraphOptions> graphOptions,
        IOptions<WorkerOptions> workerOptions)
    {
        _logger = logger;
        _graphClientFactory = graphClientFactory;
        _processedStateStore = processedStateStore;
        _attachmentTextExtractor = attachmentTextExtractor;
        _plannerClient = plannerClient;
        _graphOptions = graphOptions.Value;
        _workerOptions = workerOptions.Value;
    }

    public async Task ProcessInboxAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ProcessGraphInboxAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Graph mailbox processing failed. Trying fallback mail JSON if configured.");
            await ProcessFallbackJsonAsync(cancellationToken);
        }
    }

    private async Task ProcessGraphInboxAsync(CancellationToken cancellationToken)
    {
        var graphClient = _graphClientFactory.CreateClient();

        var response = await graphClient
            .Users[_graphOptions.MailboxAddress]
            .MailFolders[_graphOptions.MailFolderId]
            .Messages
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Top = _workerOptions.MaxMessagesPerPoll;
                requestConfiguration.QueryParameters.Select =
                [
                    "id",
                    "subject",
                    "internetMessageId",
                    "from",
                    "sender",
                    "toRecipients",
                    "ccRecipients",
                    "receivedDateTime",
                    "bodyPreview",
                    "conversationId",
                    "isRead",
                    "hasAttachments"
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

            var fullMessage = await graphClient
                .Users[_graphOptions.MailboxAddress]
                .Messages[message.Id]
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select =
                    [
                        "id",
                        "subject",
                        "internetMessageId",
                        "from",
                        "sender",
                        "toRecipients",
                        "ccRecipients",
                        "receivedDateTime",
                        "bodyPreview",
                        "body",
                        "conversationId",
                        "isRead",
                        "hasAttachments"
                    ];
                }, cancellationToken);

            if (fullMessage is null || string.IsNullOrWhiteSpace(fullMessage.Id))
            {
                _logger.LogWarning("Skipped a message because the detailed message could not be loaded. MessageId: {MessageId}", message.Id);
                continue;
            }

            var attachments = await LoadAttachmentsAsync(graphClient, fullMessage.Id, cancellationToken);
            var summary = ToSummary(fullMessage, attachments);
            var success = await HandleMessageAsync(summary, cancellationToken);

            if (!success)
            {
                continue;
            }

            if (_workerOptions.MarkAsReadAfterProcessing && fullMessage.IsRead != true)
            {
                await MarkAsReadAsync(graphClient, fullMessage.Id, cancellationToken);
            }

            await _processedStateStore.AddAsync(fullMessage.Id, cancellationToken);
        }
    }

    private async Task ProcessFallbackJsonAsync(CancellationToken cancellationToken)
    {
        var path = Path.IsPathRooted(_workerOptions.FallbackMailJsonPath)
            ? _workerOptions.FallbackMailJsonPath
            : Path.Combine(AppContext.BaseDirectory, _workerOptions.FallbackMailJsonPath);

        if (!File.Exists(path))
        {
            _logger.LogWarning("Fallback mail JSON file not found at {Path}.", path);
            return;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var summary = MailExtractionService.BuildSummaryFromGraphJson(json);
        if (summary is null)
        {
            _logger.LogWarning("Fallback mail JSON did not contain any message items.");
            return;
        }

        if (await _processedStateStore.ExistsAsync(summary.Id, cancellationToken))
        {
            _logger.LogInformation("Fallback JSON message already processed: {MessageId}", summary.Id);
            return;
        }

        _logger.LogInformation("Using fallback mail JSON for message {MessageId}.", summary.Id);
        var success = await HandleMessageAsync(summary, cancellationToken);
        if (success)
        {
            await _processedStateStore.AddAsync(summary.Id, cancellationToken);
        }
    }

    private async Task<bool> HandleMessageAsync(MailMessageSummary message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing message | Subject: {Subject} | From: {From} | Received: {Received} | ConversationId: {ConversationId}",
            message.Subject,
            message.FromAddress,
            message.ReceivedDateTime,
            message.ConversationId);

        var jdText = BuildJobDescriptionText(message);
        var extraction = MailExtractionService.ExtractFromMessage(message);

        var request = new CreatePlannerFromMailRequest
        {
            MessageId = message.Id,
            InternetMessageId = message.InternetMessageId,
            ConversationId = message.ConversationId,
            Subject = message.Subject,
            FromAddress = message.FromAddress,
            FromName = message.FromName,
            ReceivedDateTime = message.ReceivedDateTime,
            BodyPreview = message.BodyPreview,
            BodyText = TrimToMax(message.BodyText, _workerOptions.MaxBodyTextLength),
            JobDescriptionText = TrimToMax(jdText, _workerOptions.MaxAttachmentTextLength),
            HasAttachments = message.HasAttachments,
            ToRecipients = message.ToRecipients,
            CcRecipients = message.CcRecipients,
            Extraction = extraction,
            Attachments = message.Attachments.Select(x => new CreatePlannerAttachmentDto
            {
                FileName = x.FileName,
                ContentType = x.ContentType,
                Size = x.Size,
                SavedFilePath = x.SavedFilePath,
                ExtractedText = TrimToMax(x.ExtractedText, _workerOptions.MaxAttachmentTextLength),
                LooksLikeJobDescription = x.LooksLikeJobDescription
            }).ToList()
        };

        _logger.LogInformation(
            "Extracted planner values | Client: {Client} | Role: {Role} | Contact: {Contact} | Budget: {Budget} | SLA: {Sla}",
            extraction.ClientName,
            extraction.Role,
            extraction.ClientContactName,
            extraction.BudgetAmount,
            extraction.SubmissionDeadlineText);

        return await _plannerClient.CreatePlannerAsync(request, cancellationToken);
    }

    private async Task<List<MailAttachmentContent>> LoadAttachmentsAsync(GraphServiceClient graphClient, string messageId, CancellationToken cancellationToken)
    {
        var results = new List<MailAttachmentContent>();

        var response = await graphClient
            .Users[_graphOptions.MailboxAddress]
            .Messages[messageId]
            .Attachments
            .GetAsync(cancellationToken: cancellationToken);

        var attachments = response?.Value ?? [];
        if (attachments.Count == 0)
        {
            return results;
        }

        foreach (var attachment in attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attachment is not FileAttachment fileAttachment)
            {
                results.Add(new MailAttachmentContent
                {
                    AttachmentId = attachment.Id ?? string.Empty,
                    FileName = attachment.Name ?? string.Empty,
                    ContentType = attachment.ContentType ?? string.Empty,
                    Size = attachment.Size ?? 0,
                    IsInline = attachment.IsInline ?? false,
                    ExtractionStatus = $"unsupported-attachment-type:{attachment.OdataType}"
                });
                continue;
            }

            var fileName = string.IsNullOrWhiteSpace(fileAttachment.Name)
                ? $"attachment-{Guid.NewGuid():N}"
                : fileAttachment.Name;

            var savedFilePath = await SaveAttachmentAsync(messageId, fileName, fileAttachment.ContentBytes, cancellationToken);
            var (text, status) = await _attachmentTextExtractor.ExtractTextAsync(savedFilePath, cancellationToken);

            results.Add(new MailAttachmentContent
            {
                AttachmentId = fileAttachment.Id ?? string.Empty,
                FileName = fileName,
                ContentType = fileAttachment.ContentType ?? string.Empty,
                Size = fileAttachment.Size ?? 0,
                IsInline = fileAttachment.IsInline ?? false,
                SavedFilePath = savedFilePath,
                ExtractedText = text,
                ExtractionStatus = status,
                LooksLikeJobDescription = LooksLikeJobDescription(fileName, text)
            });
        }

        return results;
    }

    private async Task<string> SaveAttachmentAsync(string messageId, string fileName, byte[]? contentBytes, CancellationToken cancellationToken)
    {
        var safeMessageId = SanitizeFileName(messageId);
        var safeFileName = SanitizeFileName(fileName);

        var folderPath = Path.IsPathRooted(_workerOptions.AttachmentDownloadFolderPath)
            ? _workerOptions.AttachmentDownloadFolderPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _workerOptions.AttachmentDownloadFolderPath));

        var messageFolder = Path.Combine(folderPath, safeMessageId);
        Directory.CreateDirectory(messageFolder);

        var fullPath = Path.Combine(messageFolder, safeFileName);
        await File.WriteAllBytesAsync(fullPath, contentBytes ?? [], cancellationToken);
        return fullPath;
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

    private MailMessageSummary ToSummary(Message message, List<MailAttachmentContent> attachments)
    {
        var bodyHtml = message.Body?.Content ?? string.Empty;
        var bodyText = HtmlToPlainText(bodyHtml);

        return new MailMessageSummary
        {
            Id = message.Id ?? string.Empty,
            Subject = message.Subject ?? "(no subject)",
            InternetMessageId = message.InternetMessageId ?? string.Empty,
            FromAddress = message.From?.EmailAddress?.Address ?? message.Sender?.EmailAddress?.Address ?? "(unknown)",
            FromName = message.From?.EmailAddress?.Name ?? message.Sender?.EmailAddress?.Name ?? string.Empty,
            ReceivedDateTime = message.ReceivedDateTime,
            BodyPreview = message.BodyPreview ?? string.Empty,
            BodyHtml = bodyHtml,
            BodyText = bodyText,
            ConversationId = message.ConversationId ?? string.Empty,
            IsRead = message.IsRead ?? false,
            HasAttachments = message.HasAttachments ?? false,
            ToRecipients = message.ToRecipients?.Select(x => x.EmailAddress?.Address).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList() ?? [],
            CcRecipients = message.CcRecipients?.Select(x => x.EmailAddress?.Address).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList() ?? [],
            Attachments = attachments
        };
    }

    private string BuildJobDescriptionText(MailMessageSummary message)
    {
        var jdAttachment = message.Attachments
            .Where(x => x.LooksLikeJobDescription && !string.IsNullOrWhiteSpace(x.ExtractedText))
            .OrderByDescending(x => x.ExtractedText.Length)
            .FirstOrDefault();

        if (jdAttachment is not null)
        {
            return jdAttachment.ExtractedText;
        }

        var anyTextAttachment = message.Attachments
            .Where(x => !string.IsNullOrWhiteSpace(x.ExtractedText))
            .OrderByDescending(x => x.ExtractedText.Length)
            .FirstOrDefault();

        if (anyTextAttachment is not null)
        {
            return anyTextAttachment.ExtractedText;
        }

        return message.BodyText;
    }

    private static bool LooksLikeJobDescription(string fileName, string extractedText)
    {
        var combined = $"{fileName} {extractedText}";
        if (string.IsNullOrWhiteSpace(combined))
        {
            return false;
        }

        var lower = combined.ToLowerInvariant();
        return lower.Contains("job description")
               || lower.Contains("requirement")
               || lower.Contains("responsibilities")
               || lower.Contains("qualifications")
               || lower.Contains("skills")
               || lower.Contains("experience required");
    }

    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var noScripts = Regex.Replace(html, "<(script|style)[^>]*>.*?</\\1>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var withLineBreaks = Regex.Replace(noScripts, "<(br|/p|/div|li|/tr|/h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
        var noTags = Regex.Replace(withLineBreaks, "<[^>]+>", " ");
        var decoded = System.Net.WebUtility.HtmlDecode(noTags);
        var lines = decoded
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));

        return string.Join(Environment.NewLine, lines);
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName;
    }

    private static string TrimToMax(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
