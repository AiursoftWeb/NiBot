namespace Aiursoft.NiBot.Dedup;

internal sealed class ArchiveCancellation : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    public CancellationToken Token => _source.Token;

    public ArchiveCancellation() => Console.CancelKeyPress += Handle;

    private void Handle(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        try { _source.Cancel(); }
        catch (ObjectDisposedException) { /* A console event raced with unsubscription. */ }
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= Handle;
        _source.Dispose();
    }
}
