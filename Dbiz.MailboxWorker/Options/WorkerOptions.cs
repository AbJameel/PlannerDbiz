namespace Dbiz.MailboxWorker.Options;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public int PollIntervalSeconds { get; set; } = 60;
    public int MaxMessagesPerPoll { get; set; } = 10;
    public bool OnlyUnread { get; set; } = true;
    public bool MarkAsReadAfterProcessing { get; set; } = false;
    public string ProcessedStateFilePath { get; set; } = "Data/processed-state.json";

    public void Validate()
    {
        if (PollIntervalSeconds <= 0)
            throw new InvalidOperationException("Worker:PollIntervalSeconds must be greater than 0.");

        if (MaxMessagesPerPoll <= 0 || MaxMessagesPerPoll > 1000)
            throw new InvalidOperationException("Worker:MaxMessagesPerPoll must be between 1 and 1000.");

        if (string.IsNullOrWhiteSpace(ProcessedStateFilePath))
            throw new InvalidOperationException("Worker:ProcessedStateFilePath is required.");
    }
}
