namespace PrivacyBrowser.App;

public enum OperationFeedbackArea
{
    Home,
    Identity,
    Wallet,
    Connection,
    BrowserAndDiagnostics,
}

public enum OperationFeedbackKind
{
    Idle,
    Progress,
    Success,
    RetryableFailure,
    BlockingFailure,
}

public sealed record OperationFeedback(
    long Generation,
    OperationFeedbackArea Area,
    OperationFeedbackKind Kind,
    string Message)
{
    public static OperationFeedback Idle(OperationFeedbackArea area) =>
        new(0, area, OperationFeedbackKind.Idle, "");
}

public sealed class OperationFeedbackStore
{
    private readonly Dictionary<OperationFeedbackArea, OperationFeedback> _feedback = [];
    private long _generation;
    private long _activeGeneration;

    public OperationFeedback Get(OperationFeedbackArea area) =>
        _feedback.TryGetValue(area, out var feedback) ? feedback : OperationFeedback.Idle(area);

    public bool IsBusy => _activeGeneration != 0;

    public OperationFeedbackSession? TryStart(OperationFeedbackArea area, string message)
    {
        if (IsBusy) return null;
        var generation = NextGeneration();
        _activeGeneration = generation;
        _feedback[area] = new OperationFeedback(generation, area, OperationFeedbackKind.Progress, message);
        return new OperationFeedbackSession(this, area, generation);
    }

    internal bool Complete(
        OperationFeedbackArea area,
        long generation,
        OperationFeedbackKind kind,
        string message)
    {
        if (kind is OperationFeedbackKind.Idle or OperationFeedbackKind.Progress)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Completion feedback must be a terminal state.");
        }

        if (!_feedback.TryGetValue(area, out var current) || current.Generation != generation)
        {
            return false;
        }

        _feedback[area] = new OperationFeedback(generation, area, kind, message);
        return true;
    }

    public long Publish(OperationFeedbackArea area, OperationFeedbackKind kind, string message)
    {
        if (kind is OperationFeedbackKind.Idle or OperationFeedbackKind.Progress)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Published feedback must be a terminal state.");
        }

        var generation = NextGeneration();
        _feedback[area] = new OperationFeedback(generation, area, kind, message);
        return generation;
    }

    internal void End(long generation)
    {
        if (_activeGeneration == generation)
        {
            _activeGeneration = 0;
        }
    }

    private long NextGeneration() => ++_generation;
}

public sealed class OperationFeedbackSession : IDisposable
{
    private readonly OperationFeedbackStore _store;
    private readonly OperationFeedbackArea _area;
    private readonly long _generation;
    private bool _disposed;

    internal OperationFeedbackSession(
        OperationFeedbackStore store,
        OperationFeedbackArea area,
        long generation)
    {
        _store = store;
        _area = area;
        _generation = generation;
    }

    public bool Complete(OperationFeedbackKind kind, string message)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OperationFeedbackSession));
        return _store.Complete(_area, _generation, kind, message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _store.End(_generation);
    }
}
