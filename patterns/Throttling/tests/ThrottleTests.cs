namespace Throttling.Tests;

/// <summary>
/// What a throttle guarantees: that a resource its owner controls is protected
/// from any one consumer, and that the protection degrades before it refuses.
///
/// The degradation step is the part worth asserting. A throttle that only
/// rejects is a rate limiter facing the wrong way.
/// </summary>
public class ThrottleTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    private static (Throttle Throttle, ManualClock Clock) Build(
        int softLimit = 3, int hardLimit = 5, int windowSeconds = 60)
    {
        ManualClock clock = new(Start);
        Throttle throttle = new(
            softLimit, hardLimit, TimeSpan.FromSeconds(windowSeconds), clock);
        return (throttle, clock);
    }

    [Fact]
    public void Admits_a_tenant_within_its_soft_limit()
    {
        (Throttle throttle, _) = Build(softLimit: 3);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(ThrottleDecision.Accepted, throttle.Admit("contoso"));
        }
    }

    [Fact]
    public void Degrades_a_tenant_between_the_soft_and_hard_limits()
    {
        (Throttle throttle, _) = Build(softLimit: 3, hardLimit: 5);

        for (int i = 0; i < 3; i++)
        {
            throttle.Admit("contoso");
        }

        Assert.Equal(ThrottleDecision.Degraded, throttle.Admit("contoso"));
        Assert.Equal(ThrottleDecision.Degraded, throttle.Admit("contoso"));
    }

    [Fact]
    public void Rejects_a_tenant_beyond_its_hard_limit()
    {
        (Throttle throttle, _) = Build(softLimit: 3, hardLimit: 5);

        for (int i = 0; i < 5; i++)
        {
            throttle.Admit("contoso");
        }

        Assert.Equal(ThrottleDecision.Rejected, throttle.Admit("contoso"));
    }

    [Fact]
    public void Starts_a_fresh_allowance_in_the_next_window()
    {
        (Throttle throttle, ManualClock clock) = Build(
            softLimit: 3, hardLimit: 5, windowSeconds: 60);

        for (int i = 0; i < 6; i++)
        {
            throttle.Admit("contoso");
        }

        Assert.Equal(ThrottleDecision.Rejected, throttle.Admit("contoso"));

        clock.Advance(TimeSpan.FromSeconds(60));

        Assert.Equal(ThrottleDecision.Accepted, throttle.Admit("contoso"));
    }

    [Fact]
    public void Keeps_tenants_independent_of_one_another()
    {
        (Throttle throttle, _) = Build(softLimit: 2, hardLimit: 3);

        for (int i = 0; i < 4; i++)
        {
            throttle.Admit("noisy");
        }

        Assert.Equal(ThrottleDecision.Rejected, throttle.Admit("noisy"));
        Assert.Equal(ThrottleDecision.Accepted, throttle.Admit("quiet"));
    }
}
