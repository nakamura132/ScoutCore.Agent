namespace ScoutCore.Agent.Journal;

public interface IFileEventSource : IAsyncDisposable
{
    IAsyncEnumerable<FileEvent> ConsumeAsync(CancellationToken ct);
}
