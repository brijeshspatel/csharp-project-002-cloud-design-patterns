namespace Bulkhead.Tests;

/// <summary>
/// What a bulkhead guarantees: that one partition exhausting its pool cannot
/// take capacity from another, and that a slot is always given back.
///
/// Exhaustion is **constructed**, never raced. Slots are acquired and held
/// explicitly, so the full state is something each test creates rather than
/// something it hopes the scheduler produces. A concurrency test that depends on
/// timing is a test that fails on a busy machine and teaches people to re-run.
/// </summary>
public class BulkheadPolicyTests
{
    private static BulkheadPolicy Build() =>
        new(new Dictionary<string, int> { ["checkout"] = 2, ["reporting"] = 1 });

    [Fact]
    public async Task Runs_work_while_the_partition_has_capacity()
    {
        using BulkheadPolicy bulkhead = Build();

        string result = await bulkhead.ExecuteAsync(
            "checkout", () => Task.FromResult("order placed"));

        Assert.Equal("order placed", result);
    }

    [Fact]
    public async Task Rejects_work_once_the_partition_is_full()
    {
        using BulkheadPolicy bulkhead = Build();

        using IDisposable first = bulkhead.AcquireSlot("reporting");

        await Assert.ThrowsAsync<BulkheadRejectedException>(() =>
            bulkhead.ExecuteAsync("reporting", () => Task.FromResult("report")));
    }

    [Fact]
    public async Task Releases_the_slot_when_the_work_completes()
    {
        using BulkheadPolicy bulkhead = Build();

        await bulkhead.ExecuteAsync("reporting", () => Task.FromResult("first"));

        // The single reporting slot must be free again, or the pool leaks one
        // slot per call and the partition dies after its capacity is used once.
        string second = await bulkhead.ExecuteAsync(
            "reporting", () => Task.FromResult("second"));

        Assert.Equal("second", second);
    }

    [Fact]
    public async Task Releases_the_slot_when_the_work_throws()
    {
        using BulkheadPolicy bulkhead = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bulkhead.ExecuteAsync<string>(
                "reporting", () => throw new InvalidOperationException("report failed")));

        // This is the bug this pattern most often ships with: a slot released
        // on the success path only, so every failure permanently shrinks the
        // pool until the partition is dead.
        string afterFailure = await bulkhead.ExecuteAsync(
            "reporting", () => Task.FromResult("still working"));

        Assert.Equal("still working", afterFailure);
    }

    [Fact]
    public async Task Keeps_partitions_isolated_from_one_another()
    {
        using BulkheadPolicy bulkhead = Build();

        using IDisposable exhausted = bulkhead.AcquireSlot("reporting");

        await Assert.ThrowsAsync<BulkheadRejectedException>(() =>
            bulkhead.ExecuteAsync("reporting", () => Task.FromResult("report")));

        // Checkout is untouched by reporting's exhaustion. This is the whole
        // point of the pattern.
        string checkout = await bulkhead.ExecuteAsync(
            "checkout", () => Task.FromResult("order placed"));

        Assert.Equal("order placed", checkout);
    }

    [Fact]
    public async Task Rejects_an_unknown_partition()
    {
        using BulkheadPolicy bulkhead = Build();

        // Failing closed matters here. Admitting unknown partitions would make
        // a typo in a partition name silently unbounded, which removes exactly
        // the isolation the bulkhead was installed to provide.
        await Assert.ThrowsAsync<BulkheadRejectedException>(() =>
            bulkhead.ExecuteAsync("serch", () => Task.FromResult("typo")));
    }
}
