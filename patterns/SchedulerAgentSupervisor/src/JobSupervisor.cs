namespace SchedulerAgentSupervisor;

/// <summary>Where one step of the job has got to.</summary>
public enum JobStatus
{
    /// <summary>Not started.</summary>
    Pending,

    /// <summary>Asked, and no answer yet. The interesting state.</summary>
    Running,

    /// <summary>Done.</summary>
    Completed,

    /// <summary>Silent past its deadline twice; a person is now needed.</summary>
    Escalated,
}

/// <summary>Time, so a deadline can pass without anybody waiting for it.</summary>
public interface IClock
{
    /// <summary>The current instant.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>A clock that moves only when told to.</summary>
public sealed class ManualClock : IClock
{
    /// <summary>Creates a clock reading <paramref name="start"/>.</summary>
    public ManualClock(DateTimeOffset start) => UtcNow = start;

    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; private set; }

    /// <summary>Moves the clock forward.</summary>
    public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
}

/// <summary>
/// The agent: something remote that is asked to do one step and **may simply
/// not answer**.
///
/// Not answering is different from failing, and the difference is the reason
/// this pattern exists. A failure comes back and can be handled where it
/// happened. Silence comes back never, and the caller has no way to distinguish
/// "still working" from "gone" without a clock.
/// </summary>
public sealed class RemoteAgent
{
    private readonly Func<int, bool> answersOnAttempt;

    /// <summary>
    /// Creates an agent for <paramref name="step"/> which answers, or does not,
    /// according to which attempt this is.
    /// </summary>
    public RemoteAgent(string step, Func<int, bool> answersOnAttempt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(step);
        ArgumentNullException.ThrowIfNull(answersOnAttempt);

        Step = step;
        this.answersOnAttempt = answersOnAttempt;
    }

    /// <summary>Which step this agent performs.</summary>
    public string Step { get; }

    /// <summary>How many times it has been asked.</summary>
    public int Attempts { get; private set; }

    /// <summary>Asks the agent to work. True where it answered this time.</summary>
    public bool Ask()
    {
        Attempts++;
        return answersOnAttempt(Attempts);
    }
}

/// <summary>
/// The scheduler: it knows the steps, asks each agent to do its work, and
/// **records a deadline for every step that has not answered**.
///
/// It does not wait. Waiting is what makes a stalled step invisible; the
/// deadline is what makes it a fact somebody else can check later.
/// </summary>
public sealed class JobScheduler
{
    private sealed class StepState
    {
        public required RemoteAgent Agent { get; init; }
        public JobStatus Status { get; set; } = JobStatus.Pending;
        public DateTimeOffset Deadline { get; set; }
        public bool Retried { get; set; }
    }

    private readonly IClock clock;
    private readonly TimeSpan timeout;
    private readonly Dictionary<string, StepState> states = [];
    private readonly List<string> order = [];

    /// <summary>Creates a scheduler giving each step <paramref name="timeout"/> to answer.</summary>
    public JobScheduler(IClock clock, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeout.Ticks);

        this.clock = clock;
        this.timeout = timeout;
    }

    /// <summary>The steps, in the order they were scheduled.</summary>
    public IReadOnlyList<string> Steps => order;

    /// <summary>Adds a step and the agent that performs it.</summary>
    public void Schedule(RemoteAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        states[agent.Step] = new StepState { Agent = agent };
        order.Add(agent.Step);
    }

    /// <summary>Where <paramref name="step"/> has got to.</summary>
    public JobStatus StatusOf(string step) =>
        states.TryGetValue(step, out StepState? state) ? state.Status : JobStatus.Pending;

    /// <summary>When <paramref name="step"/> stops being merely slow.</summary>
    public DateTimeOffset DeadlineOf(string step) => states[step].Deadline;

    /// <summary>Asks every agent once, recording a deadline for any that is silent.</summary>
    public void Start()
    {
        foreach (string step in order)
        {
            Attempt(states[step]);
        }
    }

    /// <summary>Steps still awaiting an answer past <paramref name="now"/>.</summary>
    internal IEnumerable<string> OverdueAt(DateTimeOffset now) =>
        order.Where(step =>
            states[step].Status == JobStatus.Running && now >= states[step].Deadline);

    /// <summary>Whether <paramref name="step"/> has already had its one retry.</summary>
    internal bool AlreadyRetried(string step) => states[step].Retried;

    /// <summary>Asks the agent again and records a fresh deadline.</summary>
    internal void Retry(string step)
    {
        StepState state = states[step];
        state.Retried = true;
        Attempt(state);
    }

    /// <summary>Gives up on <paramref name="step"/> and hands it to a person.</summary>
    internal void Escalate(string step) => states[step].Status = JobStatus.Escalated;

    private void Attempt(StepState state)
    {
        if (state.Agent.Ask())
        {
            state.Status = JobStatus.Completed;
            return;
        }

        state.Status = JobStatus.Running;
        state.Deadline = clock.UtcNow + timeout;
    }
}

/// <summary>
/// The supervisor: the component **outside the request path** whose only job is
/// to compare deadlines against the clock.
///
/// Nothing else in the system can do this. The scheduler moved on; the agent is
/// the thing that has gone quiet; the caller is waiting. Detecting silence
/// requires a third party with a clock and no stake in the work.
///
/// It retries **once** and then escalates. Retrying for ever converts a stalled
/// step into permanent background load that nobody is looking at, which is worse
/// than a failure because it never asks for attention.
///
/// **What this is not.** Compensating Transaction is the undo; Saga makes a
/// sequence durable across a coordinator restart and assumes each step
/// eventually returns something. This pattern is about the step that returns
/// nothing at all.
/// </summary>
public sealed class JobSupervisor
{
    private readonly JobScheduler scheduler;
    private readonly IClock clock;
    private readonly List<string> escalated = [];

    /// <summary>Creates a supervisor watching <paramref name="scheduler"/>.</summary>
    public JobSupervisor(JobScheduler scheduler, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(clock);

        this.scheduler = scheduler;
        this.clock = clock;
    }

    /// <summary>How many retries this supervisor has issued.</summary>
    public int Retries { get; private set; }

    /// <summary>The steps it gave up on, named — which is the point of escalating.</summary>
    public IReadOnlyList<string> Escalated => escalated;

    /// <summary>
    /// One pass: every step overdue now is retried, or escalated where it has
    /// already had its retry. Steps still inside their deadline are left alone.
    /// </summary>
    public void Sweep()
    {
        // Materialised before acting: retrying mutates the state this reads.
        foreach (string step in scheduler.OverdueAt(clock.UtcNow).ToList())
        {
            if (scheduler.AlreadyRetried(step))
            {
                scheduler.Escalate(step);
                escalated.Add(step);
                continue;
            }

            scheduler.Retry(step);
            Retries++;
        }
    }
}
