namespace CircuitBreaker;

/// <summary>The remote dependency a checkout service calls to confirm stock.</summary>
public interface IInventoryService
{
    /// <summary>Returns the stock position for a product.</summary>
    Task<string> CheckStockAsync(string productCode);
}

/// <summary>
/// An inventory service that is down for a fixed number of calls and then
/// recovers.
///
/// Deterministic on purpose: a demonstration that failed randomly would print
/// something different every run, and a reader could not tell the breaker's
/// behaviour from the dependency's mood.
/// </summary>
public sealed class FlakyInventoryService : IInventoryService
{
    private readonly int failuresBeforeRecovery;
    private int calls;

    /// <summary>Creates a service that fails <paramref name="failuresBeforeRecovery"/> times.</summary>
    public FlakyInventoryService(int failuresBeforeRecovery) =>
        this.failuresBeforeRecovery = failuresBeforeRecovery;

    /// <summary>How many times it has actually been called.</summary>
    public int Calls => calls;

    /// <inheritdoc/>
    public Task<string> CheckStockAsync(string productCode)
    {
        calls++;
        return calls <= failuresBeforeRecovery
            ? throw new DependencyFailureException(
                $"inventory lookup for {productCode} failed (call {calls})")
            : Task.FromResult($"{productCode}: 14 in stock");
    }
}
