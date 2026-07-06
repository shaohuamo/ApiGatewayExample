namespace CommonService.Health;

public interface IStartupReadinessState
{
    bool IsReady { get; }

    string? Reason { get; }

    void MarkReady(string? reason = null);

    void MarkNotReady(string? reason);
}

public sealed class StartupReadinessState : IStartupReadinessState
{
    private readonly Lock _lock = new();
    private bool _isReady;
    private string? _reason = "Startup dependencies are not ready yet.";

    public bool IsReady
    {
        get
        {
            lock (_lock)
            {
                return _isReady;
            }
        }
    }

    public string? Reason
    {
        get
        {
            lock (_lock)
            {
                return _reason;
            }
        }
    }

    public void MarkReady(string? reason = null)
    {
        lock (_lock)
        {
            _isReady = true;
            _reason = reason;
        }
    }

    public void MarkNotReady(string? reason)
    {
        lock (_lock)
        {
            _isReady = false;
            _reason = reason;
        }
    }
}
