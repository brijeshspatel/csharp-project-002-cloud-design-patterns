namespace Bulkhead;

/// <summary>
/// Thrown when a partition has no capacity left, or does not exist.
///
/// Rejecting is the pattern working. The alternative — queueing indefinitely —
/// is how one slow dependency consumes every thread in the process, which is the
/// failure a bulkhead is installed to prevent.
/// </summary>
public sealed class BulkheadRejectedException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public BulkheadRejectedException()
        : base("The bulkhead partition has no capacity available.")
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>.</summary>
    public BulkheadRejectedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and a cause.</summary>
    public BulkheadRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Divides a shared resource into isolated pools, so that exhausting one cannot
/// starve the others.
///
/// Named <c>BulkheadPolicy</c> rather than <c>Bulkhead</c> because that is also
/// the namespace, and a type sharing its namespace's name makes every qualified
/// reference ambiguous.
///
/// The name is from shipbuilding: a hull divided into watertight compartments
/// floods one compartment when it is breached, rather than sinking. Here the
/// resource is concurrent execution, and the compartments are named partitions
/// with fixed capacities.
/// </summary>
public sealed class BulkheadPolicy : IDisposable
{
    private readonly Dictionary<string, SemaphoreSlim> pools;
    private bool disposed;

    /// <summary>Creates a bulkhead with one pool per named partition.</summary>
    /// <param name="capacities">Partition name to the concurrent work it may run.</param>
    public BulkheadPolicy(IReadOnlyDictionary<string, int> capacities)
    {
        ArgumentNullException.ThrowIfNull(capacities);

        pools = [];
        foreach ((string partition, int capacity) in capacities)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
            pools[partition] = new SemaphoreSlim(capacity, capacity);
        }
    }

    /// <summary>
    /// Takes a slot in <paramref name="partition"/>, or refuses immediately.
    ///
    /// Dispose the returned handle to give the slot back. Exposed separately
    /// from <see cref="ExecuteAsync"/> so that a caller — or a test — can hold a
    /// partition open deliberately, rather than racing to fill it.
    /// </summary>
    /// <exception cref="BulkheadRejectedException">
    /// The partition is full, or is not one this bulkhead knows about.
    /// </exception>
    public IDisposable AcquireSlot(string partition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partition);
        ObjectDisposedException.ThrowIf(disposed, this);

        if (!pools.TryGetValue(partition, out SemaphoreSlim? pool))
        {
            // Fail closed. Admitting unknown partitions would make a typo
            // silently unbounded, removing the isolation this exists to give.
            throw new BulkheadRejectedException(
                $"'{partition}' is not a configured partition.");
        }

        // Wait(0) is the whole design: take a slot if one is free, refuse at
        // once if not. Blocking here would convert exhaustion into the queue
        // the bulkhead exists to prevent.
        if (!pool.Wait(0))
        {
            throw new BulkheadRejectedException(
                $"partition '{partition}' is at capacity.");
        }

        return new Slot(pool);
    }

    /// <summary>Runs <paramref name="operation"/> inside <paramref name="partition"/>.</summary>
    public async Task<T> ExecuteAsync<T>(string partition, Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // `using` is load-bearing. Releasing after the await instead would leak
        // a slot on every failure, so each exception would permanently shrink
        // the pool until the partition was dead - the bug this pattern most
        // often ships with.
        using IDisposable slot = AcquireSlot(partition);
        return await operation().ConfigureAwait(false);
    }

    /// <summary>
    /// Releases every pool. Dispose the policy **after** every held slot has
    /// been released: a <see cref="Slot"/> disposed later releases into a
    /// disposed semaphore, which throws <see cref="ObjectDisposedException"/>.
    /// The <c>using</c> declarations in the demonstration get this right by
    /// declaring the policy first, so it is disposed last.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        foreach (SemaphoreSlim pool in pools.Values)
        {
            pool.Dispose();
        }

        disposed = true;
    }

    /// <summary>A held slot. Releasing is idempotent, so a double dispose cannot inflate a pool.</summary>
    private sealed class Slot : IDisposable
    {
        private SemaphoreSlim? pool;

        public Slot(SemaphoreSlim pool) => this.pool = pool;

        public void Dispose()
        {
            SemaphoreSlim? held = pool;
            pool = null;
            held?.Release();
        }
    }
}
