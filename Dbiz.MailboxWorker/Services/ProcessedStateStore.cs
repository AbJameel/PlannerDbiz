using System.Text.Json;
using Dbiz.MailboxWorker.Models;
using Dbiz.MailboxWorker.Options;
using Microsoft.Extensions.Options;

namespace Dbiz.MailboxWorker.Services;

public sealed class ProcessedStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ProcessedStateStore(IOptions<WorkerOptions> options)
    {
        _filePath = options.Value.ProcessedStateFilePath;
    }

    public async Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken)
    {
        var state = await LoadAsync(cancellationToken);
        return state.MessageIds.Contains(messageId);
    }

    public async Task AddAsync(string messageId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadUnsafeAsync(cancellationToken);
            state.MessageIds.Add(messageId);
            await SaveUnsafeAsync(state, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<ProcessedState> LoadAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await LoadUnsafeAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<ProcessedState> LoadUnsafeAsync(CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath();
        EnsureFolder(fullPath);

        if (!File.Exists(fullPath))
        {
            return new ProcessedState();
        }

        await using var stream = File.OpenRead(fullPath);
        var state = await JsonSerializer.DeserializeAsync<ProcessedState>(stream, cancellationToken: cancellationToken);
        return state ?? new ProcessedState();
    }

    private async Task SaveUnsafeAsync(ProcessedState state, CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath();
        EnsureFolder(fullPath);

        await using var stream = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
    }

    private string GetFullPath()
    {
        return Path.IsPathRooted(_filePath)
            ? _filePath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _filePath));
    }

    private static void EnsureFolder(string fullPath)
    {
        var folder = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }
}
