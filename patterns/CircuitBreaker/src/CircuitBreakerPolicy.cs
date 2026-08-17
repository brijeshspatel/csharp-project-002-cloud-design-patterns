namespace CircuitBreaker;

/// <summary>Which of the three states the circuit is in.</summary>
public enum CircuitState
{
    /// <summary>Calls pass through. Failures are being counted.</summary>
    Closed,

    /// <summary>Calls are refused without being attempted.</summary>
    Open,

    /// <summary>One probe is allowed through, to find out whether to close again.</summary>
    HalfOpen,
}

/// <summary>
/// Stops calling a dependency that is failing, and starts again only on evidence
/// that it has recovered.
///
/// The type is named <c>CircuitBreakerPolicy</c> rather than
/// <c>CircuitBreaker</c> because that is also the namespace, and a type sharing
/// its namespace's name makes every qualified reference ambiguous.
///
/// Where Retry asks "should I try this call again?", a breaker asks "should I be
/// calling this dependency at all right now?" — a decision about the dependency,
/// shared by every caller holding this instance. That is why the two patterns
/// pair rather than compete: retrying into a service that is down is load it
/// does not need, and the breaker is what stops it.
/// </summary>
public sealed class CircuitBreakerPolicy
{
    private readonly int failureThreshold;
    private readonly TimeSpan breakDuration;
    private readonly IClock clock;

    private int consecutiveFailures;
    private DateTimeOffset openedAt;
    private CircuitState state = CircuitState.Closed;

    /// <summary>Creates a breaker.</summary>
    /// <param name="failureThreshold">Consecutive failures that open the circuit. At least one.</param>
    /// <param name="breakDuration">How long the circuit stays open before a probe is allowed.</param>
    /// <param name="clock">Where the current time comes from.</param>
    public CircuitBreakerPolicy(int failureThreshold, TimeSpan breakDuration, IClock clock)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failureThreshold, 1);
        ArgumentNullException.ThrowIfNull(clock);

        this.failureThreshold = failureThreshold;
        this.breakDuration = breakDuration;
        this.clock = clock;
    }

    /// <summary>
    /// The current state.
    ///
    /// Reading this can *change* it: an open circuit whose break duration has
    /// elapsed becomes half-open at the moment somebody looks. The alternative
    /// is a background timer, which would make the breaker's behaviour depend on
    /// a thread nobody asked for.
    /// </summary>
    public CircuitState State
    {
        get
        {
            if (state == CircuitState.Open && clock.UtcNow - openedAt >= breakDuration)
            {
                state = CircuitState.HalfOpen;
            }

            return state;
        }
    }

    /// <summary>Runs <paramref name="operation"/> unless the circuit is open.</summary>
    /// <exception cref="CircuitOpenException">
    /// The circuit is open. <paramref name="operation"/> was **not** invoked.
    /// </exception>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        CircuitState current = State;

        if (current == CircuitState.Open)
        {
            throw new CircuitOpenException(
                $"The circuit opened at {openedAt:u} and the break duration " +
                $"of {breakDuration} has not yet elapsed.");
        }

        try
        {
            T result = await operation().ConfigureAwait(false);
            consecutiveFailures = 0;
            state = CircuitState.Closed;
            return result;
        }
        catch (DependencyFailureException)
        {
            // Only this type counts. Catching everything would open the circuit
            // on a bug in the caller, cutting off a dependency that was healthy.
            consecutiveFailures++;

            if (current == CircuitState.HalfOpen || consecutiveFailures >= failureThreshold)
            {
                state = CircuitState.Open;
                openedAt = clock.UtcNow;
            }

            throw;
        }
    }
}
