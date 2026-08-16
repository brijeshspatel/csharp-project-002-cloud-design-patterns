namespace Retry;

/// <summary>
/// Runs an operation, and tries again when it fails transiently.
///
/// Three things make the difference between this and a loop with a sleep in it,
/// and all three are worth reading the code for:
///
/// <list type="bullet">
/// <item>the delay grows, so a dependency that is struggling is not asked the
/// same question at the same rate that made it struggle;</item>
/// <item>the delay is capped, so growth does not run away into minutes;</item>
/// <item>the delay is jittered, so a thousand clients that failed together do
/// not return together and reproduce the outage they are recovering from.</item>
/// </list>
/// </summary>
public sealed class RetryPolicy
{
    private readonly int maxAttempts;
    private readonly TimeSpan baseDelay;
    private readonly TimeSpan maxDelay;
    private readonly ITimeSource time;
    private readonly Func<double> jitter;

    /// <summary>Creates a policy.</summary>
    /// <param name="maxAttempts">Total attempts, including the first. Must be at least one.</param>
    /// <param name="baseDelay">The delay before the second attempt.</param>
    /// <param name="maxDelay">The ceiling the growing delay is clamped to.</param>
    /// <param name="time">Where delays come from.</param>
    /// <param name="jitter">
    /// Returns the fraction of the computed delay actually waited, in the range
    /// zero to one. Defaults to a random fraction; the tests pin it so the
    /// schedule is assertable.
    /// </param>
    public RetryPolicy(
        int maxAttempts,
        TimeSpan baseDelay,
        TimeSpan maxDelay,
        ITimeSource time,
        Func<double>? jitter = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ArgumentNullException.ThrowIfNull(time);

        this.maxAttempts = maxAttempts;
        this.baseDelay = baseDelay;
        this.maxDelay = maxDelay;
        this.time = time;
        this.jitter = jitter ?? (() => Random.Shared.NextDouble());
    }

    /// <summary>
    /// Runs <paramref name="operation"/> until it succeeds or the attempts run
    /// out, passing it the attempt number starting at one.
    /// </summary>
    /// <exception cref="TransientFailureException">
    /// The final attempt failed transiently. It propagates unwrapped, carrying
    /// its original stack.
    /// </exception>
    public async Task<T> ExecuteAsync<T>(Func<int, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(attempt).ConfigureAwait(false);
            }
            catch (TransientFailureException) when (attempt < maxAttempts)
            {
                // The filter is doing real work. Catching unconditionally and
                // rethrowing on the last attempt would replace the original
                // stack trace with one starting here, which is exactly the
                // information somebody debugging the give-up path needs.
                await time.DelayAsync(DelayFor(attempt)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The delay to wait after a failed <paramref name="attempt"/>.</summary>
    private TimeSpan DelayFor(int attempt)
    {
        double exponential = baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        double capped = Math.Min(exponential, maxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(capped * jitter());
    }
}
