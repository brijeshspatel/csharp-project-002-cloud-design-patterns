namespace ComputeResourceConsolidation.Tests;

/// <summary>
/// What consolidation guarantees: that many small tasks run on **one**
/// computational unit instead of one each — and that the tasks themselves are
/// unchanged by the move.
///
/// The cost is asserted just as directly. Sharing a host means sharing its
/// capacity, so one greedy task degrades its neighbours. That is the trade this
/// pattern makes, and a model that hid it would be advertising rather than
/// teaching.
/// </summary>
public class ConsolidatedHostTests
{
    private static ConsolidatedHost HostWithFiveTasks()
    {
        ConsolidatedHost host = new(capacity: 100);
        host.Add(new ScheduledTask("nightly-report", cost: 10));
        host.Add(new ScheduledTask("cache-warm", cost: 10));
        host.Add(new ScheduledTask("index-rebuild", cost: 10));
        host.Add(new ScheduledTask("licence-sweep", cost: 10));
        host.Add(new ScheduledTask("audit-export", cost: 10));
        return host;
    }

    [Fact]
    public void Runs_every_task_on_one_host()
    {
        ConsolidatedHost host = HostWithFiveTasks();

        host.RunAll();

        Assert.Equal(1, host.Usage.UnitsAfter);
        Assert.All(host.Tasks, task => Assert.Equal(1, host.RunsOf(task)));
    }

    [Fact]
    public void Counts_the_compute_units_consolidation_removed()
    {
        ConsolidatedHost host = HostWithFiveTasks();

        ResourceUsage usage = host.Usage;

        // Five tasks that each had a host of their own now share one: four
        // units that no longer have to exist, be patched, or be paid for.
        Assert.Equal(5, usage.UnitsBefore);
        Assert.Equal(1, usage.UnitsAfter);
        Assert.Equal(4, usage.UnitsRemoved);
    }

    [Fact]
    public void Runs_the_same_tasks_the_separate_hosts_ran()
    {
        ConsolidatedHost host = HostWithFiveTasks();

        Assert.Equal(5, host.RunAll().Count);

        // And still every one of them when the host is over-committed, which is
        // the only case where shedding is even tempting. Consolidation moves
        // work; it does not quietly drop what does not fit, because a host that
        // did would look efficient and be losing the nightly report.
        host.Add(new ScheduledTask("bulk-import", cost: 60));

        IReadOnlyList<string> results = host.RunAll();

        Assert.True(host.IsOverCommitted);
        Assert.Equal(6, results.Count);
        Assert.Contains(results, line => line.Contains("nightly-report", StringComparison.Ordinal));
        Assert.Contains(results, line => line.Contains("bulk-import", StringComparison.Ordinal));
    }

    [Fact]
    public void Lets_one_task_affect_its_neighbours()
    {
        ConsolidatedHost host = HostWithFiveTasks();

        Assert.False(host.IsOverCommitted);
        Assert.All(host.RunAll(), line => Assert.DoesNotContain("degraded", line, StringComparison.Ordinal));

        host.Add(new ScheduledTask("bulk-import", cost: 60));

        // The import did not fail; everything else got slower. Shared capacity
        // means shared fate, which is the price of the units removed above.
        Assert.True(host.IsOverCommitted);
        Assert.All(host.RunAll(), line => Assert.Contains("degraded", line, StringComparison.Ordinal));
    }

    [Fact]
    public void Reports_the_utilisation_of_the_consolidated_host()
    {
        ConsolidatedHost host = HostWithFiveTasks();

        // Fifty per cent: the number that says whether consolidating further is
        // sensible or reckless.
        Assert.Equal(50, host.Usage.Utilisation);

        host.Add(new ScheduledTask("bulk-import", cost: 60));

        Assert.Equal(110, host.Usage.Utilisation);
    }
}
