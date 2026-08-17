namespace CircuitBreaker.Tests;

/// <summary>
/// What a circuit breaker guarantees: that a dependency which is failing stops
/// being called, and starts being called again only once there is evidence it
/// has recovered.
///
/// Every test drives a <see cref="ManualClock"/>, so the break duration passes
/// when the test says it does rather than when the machine gets round to it.
/// </summary>
public class CircuitBreakerPolicyTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    private static (CircuitBreakerPolicy Breaker, ManualClock Clock) Build(
        int failureThreshold = 3, int breakSeconds = 30)
    {
        ManualClock clock = new(Start);
        CircuitBreakerPolicy breaker = new(
            failureThreshold,
            TimeSpan.FromSeconds(breakSeconds),
            clock);
        return (breaker, clock);
    }

    private static Task<string> Fails() =>
        throw new DependencyFailureException("inventory is unavailable");

    [Fact]
    public async Task Passes_calls_through_while_closed()
    {
        (CircuitBreakerPolicy breaker, _) = Build();

        string result = await breaker.ExecuteAsync(() => Task.FromResult("in stock"));

        Assert.Equal("in stock", result);
        Assert.Equal(CircuitState.Closed, breaker.State);
    }

    [Fact]
    public async Task Opens_after_the_failure_threshold_is_reached()
    {
        (CircuitBreakerPolicy breaker, _) = Build(failureThreshold: 3);

        for (int i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<DependencyFailureException>(
                () => breaker.ExecuteAsync(Fails));
        }

        Assert.Equal(CircuitState.Open, breaker.State);
    }

    [Fact]
    public async Task Fails_fast_without_calling_the_service_while_open()
    {
        (CircuitBreakerPolicy breaker, _) = Build(failureThreshold: 2);
        int calls = 0;

        for (int i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<DependencyFailureException>(
                () => breaker.ExecuteAsync(Fails));
        }

        await Assert.ThrowsAsync<CircuitOpenException>(() =>
            breaker.ExecuteAsync(() =>
            {
                calls++;
                return Task.FromResult("never reached");
            }));

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Moves_to_half_open_once_the_break_duration_has_elapsed()
    {
        (CircuitBreakerPolicy breaker, ManualClock clock) = Build(
            failureThreshold: 2, breakSeconds: 30);

        for (int i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<DependencyFailureException>(
                () => breaker.ExecuteAsync(Fails));
        }

        Assert.Equal(CircuitState.Open, breaker.State);

        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(CircuitState.HalfOpen, breaker.State);
    }

    [Fact]
    public async Task A_successful_probe_closes_the_circuit_and_resets_the_count()
    {
        (CircuitBreakerPolicy breaker, ManualClock clock) = Build(
            failureThreshold: 2, breakSeconds: 30);

        for (int i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<DependencyFailureException>(
                () => breaker.ExecuteAsync(Fails));
        }

        clock.Advance(TimeSpan.FromSeconds(30));

        string result = await breaker.ExecuteAsync(() => Task.FromResult("recovered"));

        Assert.Equal("recovered", result);
        Assert.Equal(CircuitState.Closed, breaker.State);

        // One failure must not reopen it: the count was reset, so the threshold
        // of two has not been reached again.
        await Assert.ThrowsAsync<DependencyFailureException>(
            () => breaker.ExecuteAsync(Fails));
        Assert.Equal(CircuitState.Closed, breaker.State);
    }

    [Fact]
    public async Task A_failed_probe_reopens_the_circuit_and_restarts_the_clock()
    {
        (CircuitBreakerPolicy breaker, ManualClock clock) = Build(
            failureThreshold: 2, breakSeconds: 30);

        for (int i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<DependencyFailureException>(
                () => breaker.ExecuteAsync(Fails));
        }

        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(CircuitState.HalfOpen, breaker.State);

        await Assert.ThrowsAsync<DependencyFailureException>(
            () => breaker.ExecuteAsync(Fails));

        Assert.Equal(CircuitState.Open, breaker.State);

        // The break duration restarts from the failed probe, so almost all of
        // it must pass again before another probe is allowed.
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(CircuitState.Open, breaker.State);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(CircuitState.HalfOpen, breaker.State);
    }

    [Fact]
    public async Task A_success_resets_the_failure_count_before_the_threshold()
    {
        (CircuitBreakerPolicy breaker, _) = Build(failureThreshold: 3);

        for (int i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<DependencyFailureException>(
                () => breaker.ExecuteAsync(Fails));
        }

        await breaker.ExecuteAsync(() => Task.FromResult("fine"));

        for (int i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<DependencyFailureException>(
                () => breaker.ExecuteAsync(Fails));
        }

        Assert.Equal(CircuitState.Closed, breaker.State);
    }
}
