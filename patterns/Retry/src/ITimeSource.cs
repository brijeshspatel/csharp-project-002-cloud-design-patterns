namespace Retry;

/// <summary>
/// Where a delay comes from.
///
/// Injected rather than called directly, because a retry policy that can only
/// wait in real time can only be tested in real time. Every delay schedule this
/// pattern promises -- exponential growth, the cap, the jitter window -- is
/// only assertable because the waiting is somebody else's job.
///
/// This lives in this pattern's own folder rather than in a shared location.
/// Tier 1's other patterns each carry their own; the duplication is deliberate,
/// and it is what lets one folder be lifted out without unpicking it from the
/// rest.
/// </summary>
public interface ITimeSource
{
    /// <summary>Waits for <paramref name="duration"/>.</summary>
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default);
}

/// <summary>
/// Records what it was asked to wait for, and returns at once.
///
/// Used by the tests and by the demonstration. It is the reason the
/// demonstration finishes instantly instead of sleeping for the better part of
/// a minute, and the reason nothing in this pattern's folder is timing
/// dependent.
/// </summary>
public sealed class RecordingTimeSource : ITimeSource
{
    private readonly List<TimeSpan> delays = [];

    /// <summary>Every delay asked for, in the order it was asked for.</summary>
    public IReadOnlyList<TimeSpan> Delays => delays;

    /// <inheritdoc/>
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        delays.Add(duration);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Waits for real. Not used by the tests or the demonstration, and present
/// because a reader should be able to see what the production implementation
/// of this interface actually is.
/// </summary>
public sealed class RealTimeSource : ITimeSource
{
    /// <inheritdoc/>
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default) =>
        Task.Delay(duration, cancellationToken);
}
