namespace Retry;

/// <summary>
/// A dependency that sometimes fails. Stands in for the remote call a retry
/// policy exists to wrap.
/// </summary>
public interface IUnreliableService
{
    /// <summary>Calls the service.</summary>
    Task<string> CallAsync();
}

/// <summary>
/// Fails a fixed number of times, then succeeds.
///
/// Deterministic on purpose. A demonstration that failed randomly would print
/// something different every run, and a reader could not tell the pattern's
/// behaviour from the dependency's mood.
/// </summary>
public sealed class FlakyService : IUnreliableService
{
    private readonly int failuresBeforeSuccess;
    private int calls;

    /// <summary>Creates a service that fails <paramref name="failuresBeforeSuccess"/> times first.</summary>
    public FlakyService(int failuresBeforeSuccess) =>
        this.failuresBeforeSuccess = failuresBeforeSuccess;

    /// <summary>How many times it has been called.</summary>
    public int Calls => calls;

    /// <inheritdoc/>
    public Task<string> CallAsync()
    {
        calls++;
        return calls <= failuresBeforeSuccess
            ? throw new TransientFailureException(
                $"the dependency is not ready (call {calls})")
            : Task.FromResult("payload");
    }
}

/// <summary>Never recovers. Stands in for an outage rather than a blip.</summary>
public sealed class BrokenService : IUnreliableService
{
    private int calls;

    /// <summary>How many times it has been called.</summary>
    public int Calls => calls;

    /// <inheritdoc/>
    public Task<string> CallAsync()
    {
        calls++;
        throw new TransientFailureException($"the dependency is down (call {calls})");
    }
}
