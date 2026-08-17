namespace CompensatingTransaction;

/// <summary>
/// One step of a longer operation, together with the action that counters it.
///
/// **The compensation is a new action, not a rollback.** It runs against a
/// system that has already accepted the original and may refuse, be unavailable,
/// or have moved on — which is why it returns whether it succeeded rather than
/// being assumed to.
/// </summary>
public sealed class BookingStep
{
    private readonly Func<bool> compensate;

    /// <summary>Creates a step and the action that counters it.</summary>
    public BookingStep(string name, Func<bool> perform, Func<bool> compensate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(perform);
        ArgumentNullException.ThrowIfNull(compensate);

        Name = name;
        Perform = perform;
        this.compensate = compensate;
        CanCompensate = true;
    }

    private BookingStep(string name, Func<bool> perform)
    {
        Name = name;
        Perform = perform;
        compensate = () => false;
        CanCompensate = false;
    }

    /// <summary>What this step is called.</summary>
    public string Name { get; }

    /// <summary>Doing the work. True where it succeeded.</summary>
    public Func<bool> Perform { get; }

    /// <summary>Whether anything at all can counter this step.</summary>
    public bool CanCompensate { get; }

    /// <summary>
    /// A step with **no compensation that exists** — an email already sent, a
    /// message already delivered, a payment already settled to a third party.
    /// Modelling this is the honest part of the pattern: some things cannot be
    /// undone, and a workflow that pretends otherwise is lying to its caller.
    /// </summary>
    public static BookingStep WithoutCompensation(string name, Func<bool> perform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(perform);
        return new BookingStep(name, perform);
    }

    /// <summary>Runs the compensation. False where the world was not restored.</summary>
    public bool Compensate() => CanCompensate && compensate();
}

/// <summary>One thing that happened, in the order it happened.</summary>
/// <param name="Step">Which step.</param>
/// <param name="WasCompensation">Whether this was the undo rather than the do.</param>
/// <param name="Succeeded">Whether it worked.</param>
public readonly record struct StepRecord(string Step, bool WasCompensation, bool Succeeded);

/// <summary>What the workflow ended up doing.</summary>
/// <param name="Succeeded">Whether every step completed.</param>
/// <param name="FailedStep">The step that failed, where one did.</param>
/// <param name="NotUndone">Steps whose effects remain, because compensation failed or does not exist.</param>
public sealed record BookingOutcome(bool Succeeded, string? FailedStep, IReadOnlyList<string> NotUndone);

/// <summary>
/// Runs a sequence of steps, and counters the completed ones **in reverse
/// order** when a later step fails.
///
/// Reverse order is not a stylistic choice: later steps generally depend on
/// earlier ones, so releasing the earlier resource first can leave the later
/// undo with nothing to act on. Undo in the order things were done and the
/// dependency runs the wrong way.
///
/// **This pattern is about the undo, not about who drives the steps.** Saga is
/// what makes the sequence and its compensations durable across a restart;
/// Scheduler Agent Supervisor is what notices a step that never answered at all.
/// </summary>
public sealed class TripBooking
{
    private readonly IReadOnlyList<BookingStep> steps;
    private readonly List<StepRecord> history = [];

    /// <summary>Creates a workflow over <paramref name="steps"/>, in order.</summary>
    public TripBooking(IReadOnlyList<BookingStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        this.steps = steps;
    }

    /// <summary>Everything that happened, in order — the do and the undo alike.</summary>
    public IReadOnlyList<StepRecord> History => history;

    /// <summary>
    /// Runs every step. On the first failure, counters the steps that completed,
    /// last first, and reports whatever could not be countered.
    /// </summary>
    public BookingOutcome Book()
    {
        List<BookingStep> completed = [];

        foreach (BookingStep step in steps)
        {
            bool done = step.Perform();
            history.Add(new StepRecord(step.Name, WasCompensation: false, done));

            if (!done)
            {
                // The failed step did nothing, so there is nothing to counter.
                // Compensating it would be a second, unrelated change.
                return Unwind(completed, step.Name);
            }

            completed.Add(step);
        }

        return new BookingOutcome(Succeeded: true, FailedStep: null, NotUndone: []);
    }

    private BookingOutcome Unwind(List<BookingStep> completed, string failedStep)
    {
        List<string> notUndone = [];

        for (int i = completed.Count - 1; i >= 0; i--)
        {
            BookingStep step = completed[i];
            bool undone = step.Compensate();
            history.Add(new StepRecord(step.Name, WasCompensation: true, undone));

            if (!undone)
            {
                notUndone.Add(step.Name);
            }
        }

        return new BookingOutcome(Succeeded: false, failedStep, notUndone);
    }
}
