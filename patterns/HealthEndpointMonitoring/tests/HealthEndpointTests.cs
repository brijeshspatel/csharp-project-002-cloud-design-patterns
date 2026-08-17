namespace HealthEndpointMonitoring.Tests;

/// <summary>
/// What a health endpoint guarantees: that it reports the worst thing it found,
/// that a broken check cannot take the endpoint down with it, and that a probe
/// which examined nothing does not claim health.
/// </summary>
public class HealthEndpointTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    private sealed class StubCheck : IHealthCheck
    {
        private readonly Func<HealthCheckResult> result;

        public StubCheck(string name, Func<HealthCheckResult> result)
        {
            Name = name;
            this.result = result;
        }

        public string Name { get; }

        public Task<HealthCheckResult> CheckAsync() => Task.FromResult(result());
    }

    private static StubCheck Healthy(string name) =>
        new(name, () => new HealthCheckResult(HealthStatus.Healthy, "fine"));

    private static StubCheck Degraded(string name) =>
        new(name, () => new HealthCheckResult(HealthStatus.Degraded, "slow"));

    private static StubCheck Unhealthy(string name) =>
        new(name, () => new HealthCheckResult(HealthStatus.Unhealthy, "down"));

    private static StubCheck Throws(string name) =>
        new(name, () => throw new InvalidOperationException("the check itself is broken"));

    private static HealthEndpoint Endpoint(ManualClock clock, params IHealthCheck[] checks) =>
        new(checks, TimeSpan.FromMilliseconds(500), clock);

    [Fact]
    public async Task Reports_healthy_when_every_check_is_healthy()
    {
        ManualClock clock = new(Start);
        HealthReport report = await Endpoint(clock, Healthy("db"), Healthy("cache")).ProbeAsync();

        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task Reports_degraded_when_the_worst_check_is_degraded()
    {
        ManualClock clock = new(Start);
        HealthReport report = await Endpoint(clock, Healthy("db"), Degraded("cache")).ProbeAsync();

        Assert.Equal(HealthStatus.Degraded, report.Status);
    }

    [Fact]
    public async Task Reports_unhealthy_when_any_check_is_unhealthy()
    {
        ManualClock clock = new(Start);

        // The unhealthy check is last, so a fold that returned the first status
        // rather than the worst would report Healthy and pass every other test.
        HealthReport report = await Endpoint(
            clock, Healthy("db"), Degraded("cache"), Unhealthy("broker")).ProbeAsync();

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }

    [Fact]
    public async Task Treats_a_check_that_throws_as_unhealthy_without_propagating()
    {
        ManualClock clock = new(Start);

        HealthReport report = await Endpoint(clock, Healthy("db"), Throws("cache")).ProbeAsync();

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Contains(report.Entries, e => e.Name == "cache" &&
            e.Status == HealthStatus.Unhealthy &&
            e.Detail.Contains("broken", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Marks_a_slow_check_degraded_rather_than_failed()
    {
        ManualClock clock = new(Start);

        // The check advances the clock while it runs, which is how a slow
        // dependency is modelled without anything actually sleeping.
        IHealthCheck slow = new StubCheck("db", () =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(900));
            return new HealthCheckResult(HealthStatus.Healthy, "fine, eventually");
        });

        HealthReport report = await Endpoint(clock, slow).ProbeAsync();

        Assert.Equal(HealthStatus.Degraded, report.Status);
        Assert.Equal(HealthStatus.Degraded, Assert.Single(report.Entries).Status);
    }

    [Fact]
    public async Task Reports_unhealthy_when_there_is_nothing_to_check()
    {
        ManualClock clock = new(Start);

        HealthReport report = await Endpoint(clock).ProbeAsync();

        // A probe that examined nothing is not evidence of health. Reporting
        // Healthy here is the "passes while measuring nothing" failure wearing
        // a different hat, and it is the one an operator would never catch.
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }
}
