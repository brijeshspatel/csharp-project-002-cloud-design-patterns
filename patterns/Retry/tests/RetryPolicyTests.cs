namespace Retry.Tests;

/// <summary>
/// What a retry policy guarantees, rather than how this one is written.
///
/// Every test drives a <see cref="RecordingTimeSource"/>, so none of them waits
/// for real time to pass. That is what makes the delay schedule assertable at
/// all: a policy that could only wait in real time could only be tested by a
/// suite that really took eight seconds to run.
/// </summary>
public class RetryPolicyTests
{
    private static readonly int[] FirstThreeAttempts = [1, 2, 3];

    private static RetryPolicy Policy(
        RecordingTimeSource time, int maxAttempts = 4, double jitter = 1.0) =>
        new(maxAttempts,
            baseDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromSeconds(2),
            time: time,
            jitter: () => jitter);

    [Fact]
    public async Task Succeeds_on_the_first_attempt_without_delaying()
    {
        RecordingTimeSource time = new();
        int calls = 0;

        string result = await Policy(time).ExecuteAsync(_ =>
        {
            calls++;
            return Task.FromResult("ok");
        });

        Assert.Equal("ok", result);
        Assert.Equal(1, calls);
        Assert.Empty(time.Delays);
    }

    [Fact]
    public async Task Retries_until_the_operation_succeeds()
    {
        RecordingTimeSource time = new();
        int calls = 0;

        string result = await Policy(time).ExecuteAsync(_ =>
        {
            calls++;
            return calls < 3
                ? throw new TransientFailureException("still warming up")
                : Task.FromResult("ok");
        });

        Assert.Equal("ok", result);
        Assert.Equal(3, calls);
        Assert.Equal(2, time.Delays.Count);
    }

    [Fact]
    public async Task Stops_after_the_maximum_number_of_attempts()
    {
        RecordingTimeSource time = new();
        int calls = 0;

        await Assert.ThrowsAsync<TransientFailureException>(() =>
            Policy(time, maxAttempts: 4).ExecuteAsync<string>(_ =>
            {
                calls++;
                throw new TransientFailureException("down");
            }));

        Assert.Equal(4, calls);
        Assert.Equal(3, time.Delays.Count);
    }

    [Fact]
    public async Task Delays_grow_exponentially()
    {
        RecordingTimeSource time = new();

        await Assert.ThrowsAsync<TransientFailureException>(() =>
            Policy(time, maxAttempts: 4).ExecuteAsync<string>(_ =>
                throw new TransientFailureException("down")));

        Assert.Equal(
            new[]
            {
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromMilliseconds(400),
            },
            time.Delays);
    }

    [Fact]
    public async Task No_delay_ever_exceeds_the_cap()
    {
        RecordingTimeSource time = new();

        await Assert.ThrowsAsync<TransientFailureException>(() =>
            Policy(time, maxAttempts: 10).ExecuteAsync<string>(_ =>
                throw new TransientFailureException("down")));

        Assert.Equal(9, time.Delays.Count);
        Assert.All(time.Delays, delay => Assert.True(delay <= TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Does_not_retry_a_failure_it_was_not_told_to_retry()
    {
        RecordingTimeSource time = new();
        int calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Policy(time).ExecuteAsync<string>(_ =>
            {
                calls++;
                throw new InvalidOperationException("a bug, not a blip");
            }));

        Assert.Equal(1, calls);
        Assert.Empty(time.Delays);
    }

    [Fact]
    public async Task Jitter_scales_each_delay_within_its_window()
    {
        RecordingTimeSource time = new();

        await Assert.ThrowsAsync<TransientFailureException>(() =>
            Policy(time, maxAttempts: 3, jitter: 0.5).ExecuteAsync<string>(_ =>
                throw new TransientFailureException("down")));

        Assert.Equal(
            new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100) },
            time.Delays);
    }

    [Fact]
    public async Task Reports_the_attempt_number_to_the_operation()
    {
        RecordingTimeSource time = new();
        List<int> seen = [];

        await Assert.ThrowsAsync<TransientFailureException>(() =>
            Policy(time, maxAttempts: 3).ExecuteAsync<string>(attempt =>
            {
                seen.Add(attempt);
                throw new TransientFailureException("down");
            }));

        Assert.Equal(FirstThreeAttempts, seen);
    }
}
