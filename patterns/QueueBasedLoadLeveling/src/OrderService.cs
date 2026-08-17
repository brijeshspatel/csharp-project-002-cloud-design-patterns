namespace QueueBasedLoadLeveling;

/// <summary>An order placed by a customer.</summary>
/// <param name="Reference">What the customer quotes when they ring up.</param>
public readonly record struct Order(string Reference);

/// <summary>Thrown when a service is asked for more than it can take.</summary>
public sealed class ServiceOverloadedException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public ServiceOverloadedException()
        : base("The service is at capacity for this cycle.")
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>.</summary>
    public ServiceOverloadedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and a cause.</summary>
    public ServiceOverloadedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A downstream service with a hard limit on what it can take at once.
///
/// The limit is the reason this pattern exists. Real services have one whether
/// or not they advertise it — a connection pool, a thread pool, a provisioned
/// throughput — and load arriving above it is refused rather than queued.
/// </summary>
public sealed class OrderService
{
    private readonly List<Order> accepted = [];
    private int acceptedThisCycle;

    /// <summary>Creates a service that accepts <paramref name="perCycleCapacity"/> per cycle.</summary>
    public OrderService(int perCycleCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(perCycleCapacity, 1);
        PerCycleCapacity = perCycleCapacity;
    }

    /// <summary>How many orders it can take in one cycle.</summary>
    public int PerCycleCapacity { get; }

    /// <summary>Everything it has accepted, in the order it accepted it.</summary>
    public IReadOnlyList<Order> Accepted => accepted;

    /// <summary>How many more it will take this cycle.</summary>
    public int RemainingThisCycle => PerCycleCapacity - acceptedThisCycle;

    /// <summary>Starts a fresh cycle, restoring the full allowance.</summary>
    public void BeginCycle() => acceptedThisCycle = 0;

    /// <summary>Takes an order, or refuses it.</summary>
    /// <exception cref="ServiceOverloadedException">The cycle's capacity is used up.</exception>
    public void Accept(Order order)
    {
        if (acceptedThisCycle >= PerCycleCapacity)
        {
            throw new ServiceOverloadedException(
                $"the service takes {PerCycleCapacity} per cycle and has taken {acceptedThisCycle}");
        }

        acceptedThisCycle++;
        accepted.Add(order);
    }
}
