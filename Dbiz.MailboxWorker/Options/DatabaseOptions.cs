namespace Dbiz.MailboxWorker.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool Enabled { get; set; } = true;
    public string ConnectionString { get; set; } = string.Empty;

    public void Validate()
    {
        if (Enabled && string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException("Database:ConnectionString is required when database loading is enabled.");
        }
    }
}
