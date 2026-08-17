namespace Saga;

/// <summary>Where a step, or the saga itself, has got to.</summary>
public enum SagaStatus
{
    /// <summary>Attempted, outcome not yet recorded.</summary>
    Started,

    /// <summary>Done, and not to be done again.</summary>
    Completed,

    /// <summary>Countered after a later step failed.</summary>
    Compensated,

    /// <summary>
    /// Countering was attempted and reported failure — the step's effect may
    /// still stand. Recorded rather than swallowed: a log that shows
    /// <see cref="Compensated"/> for a compensation that failed lies to the
    /// operator who reads it, and to the replacement coordinator that trusts it.
    /// </summary>
    CompensationFailed,

    /// <summary>The saga gave up; everything completed has been countered.</summary>
    Failed,
}

/// <summary>One line of the log.</summary>
/// <param name="Step">Which step.</param>
/// <param name="Status">What happened to it.</param>
public readonly record struct SagaEntry(string Step, SagaStatus Status);

/// <summary>
/// The durable record of what the saga has done — **written as it happens, not
/// at the end**.
///
/// This is the whole difference between a saga and a loop with a
/// <c>catch</c>. A coordinator that crashes takes its call stack, its local
/// variables and its intentions with it; the log is what a replacement reads to
/// discover that the card was charged and the stock reserved, so that it repeats
/// neither.
///
/// Written afterwards it records only sagas that did not need it.
/// </summary>
public sealed class SagaLog
{
    private readonly List<SagaEntry> entries = [];

    /// <summary>Every line, oldest first.</summary>
    public IReadOnlyList<SagaEntry> Entries => entries;

    /// <summary>Appends a line. The only way the log changes.</summary>
    public void Append(SagaEntry entry) => entries.Add(entry);

    /// <summary>Whether <paramref name="step"/> is recorded as done and not since countered.</summary>
    public bool IsComplete(string step)
    {
        bool complete = false;

        foreach (SagaEntry entry in entries)
        {
            if (!string.Equals(entry.Step, step, StringComparison.Ordinal))
            {
                continue;
            }

            complete = entry.Status switch
            {
                SagaStatus.Completed => true,
                SagaStatus.Compensated => false,
                _ => complete,
            };
        }

        return complete;
    }
}

/// <summary>
/// One business transaction spanning three services — payment, inventory,
/// shipping — with its compensations, held together by a log.
///
/// **The steps are fixed, deliberately.** A saga is a named business
/// transaction rather than a general mechanism for running arbitrary work: it
/// is the thing that knows that reserving stock comes after taking payment and
/// what each of those means. Compensating Transaction is the general mechanism,
/// and this pattern does not import it — the sequence and the log are what this
/// one is about.
///
/// **What this is not.** Scheduler Agent Supervisor is what notices a step that
/// never answered at all — a saga assumes each step eventually returns
/// something. Choreography is what this looks like with nobody holding the
/// sequence.
/// </summary>
public sealed class OrderSaga
{
    // Fixed at class scope rather than inline: a constant array argument is
    // allocated on every call, which CA1861 objects to and which is pointless
    // for a sequence that never varies.
    private static readonly string[] Steps = ["payment", "inventory", "shipping"];

    private readonly SagaLog log;
    private readonly Func<string, bool> perform;
    private readonly Func<string, bool> compensate;

    /// <summary>Creates a saga recording into <paramref name="log"/>.</summary>
    public OrderSaga(SagaLog log, Func<string, bool> perform, Func<string, bool> compensate)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(perform);
        ArgumentNullException.ThrowIfNull(compensate);

        this.log = log;
        this.perform = perform;
        this.compensate = compensate;
    }

    /// <summary>
    /// Runs every step the log does not already show complete, compensating
    /// what has completed if one fails.
    /// </summary>
    public SagaStatus Run() => RunUpTo(null);

    /// <summary>
    /// Runs as far as <paramref name="lastStep"/> and then stops without
    /// finishing — how this model represents a coordinator that was lost
    /// mid-run. Everything it did is in the log; nothing else survives it.
    /// </summary>
    public SagaStatus RunUpTo(string? lastStep)
    {
        foreach (string step in Steps)
        {
            if (log.IsComplete(step))
            {
                continue;
            }

            log.Append(new SagaEntry(step, SagaStatus.Started));

            if (!perform(step))
            {
                Unwind();
                log.Append(new SagaEntry(step, SagaStatus.Failed));
                return SagaStatus.Failed;
            }

            log.Append(new SagaEntry(step, SagaStatus.Completed));

            if (string.Equals(step, lastStep, StringComparison.Ordinal))
            {
                return SagaStatus.Started;
            }
        }

        return SagaStatus.Completed;
    }

    private void Unwind()
    {
        for (int i = Steps.Length - 1; i >= 0; i--)
        {
            string step = Steps[i];
            if (!log.IsComplete(step))
            {
                continue;
            }

            // The result is recorded, not assumed. A step whose compensation
            // failed still stands, and IsComplete keeps saying so — which is
            // what lets a later coordinator attempt the compensation again.
            log.Append(new SagaEntry(
                step,
                compensate(step) ? SagaStatus.Compensated : SagaStatus.CompensationFailed));
        }
    }
}
