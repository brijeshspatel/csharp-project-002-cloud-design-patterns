namespace SchedulerAgentSupervisor.Tests;

/// <summary>
/// What this pattern guarantees: that a step which **never answered at all** is
/// noticed, retried once, and then escalated — by something outside the request
/// path, watching a clock.
///
/// Silence is the subject, not failure. A step that returns an error is the easy
/// case, and a saga already handles it; a step that accepts the work and then
/// goes away leaves a coordinator waiting for ever unless somebody is timing it.
/// </summary>
public class JobSupervisorTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(5);

    /// <summary>An agent that always answers.</summary>
    private static RemoteAgent Answers(string name) => new(name, _ => true);

    /// <summary>An agent that never answers, however often it is asked.</summary>
    private static RemoteAgent Silent(string name) => new(name, _ => false);

    /// <summary>An agent that is silent first and answers when asked again.</summary>
    private static RemoteAgent AnswersOnRetry(string name) => new(name, attempt => attempt > 1);

    [Fact]
    public void Completes_a_job_whose_agents_all_answer()
    {
        ManualClock clock = new(Noon);
        JobScheduler scheduler = new(clock, Deadline);
        scheduler.Schedule(Answers("probe"));
        scheduler.Schedule(Answers("transcode"));

        scheduler.Start();

        Assert.Equal(JobStatus.Completed, scheduler.StatusOf("probe"));
        Assert.Equal(JobStatus.Completed, scheduler.StatusOf("transcode"));
    }

    [Fact]
    public void Leaves_a_silent_step_running_until_its_deadline()
    {
        ManualClock clock = new(Noon);
        JobScheduler scheduler = new(clock, Deadline);
        scheduler.Schedule(Silent("transcode"));
        scheduler.Start();

        JobSupervisor supervisor = new(scheduler, clock);
        clock.Advance(TimeSpan.FromMinutes(4));
        supervisor.Sweep();

        // Four minutes of silence is not yet a problem. A supervisor that acts
        // early turns slow into failed, and duplicates work that was going to
        // finish.
        Assert.Equal(JobStatus.Running, scheduler.StatusOf("transcode"));
        Assert.Equal(0, supervisor.Retries);
    }

    [Fact]
    public void Retries_a_step_whose_deadline_has_passed()
    {
        ManualClock clock = new(Noon);
        JobScheduler scheduler = new(clock, Deadline);
        RemoteAgent agent = AnswersOnRetry("transcode");
        scheduler.Schedule(agent);
        scheduler.Start();

        JobSupervisor supervisor = new(scheduler, clock);
        clock.Advance(TimeSpan.FromMinutes(6));
        supervisor.Sweep();

        Assert.Equal(1, supervisor.Retries);
        Assert.Equal(2, agent.Attempts);
        Assert.Equal(JobStatus.Completed, scheduler.StatusOf("transcode"));
    }

    [Fact]
    public void Escalates_a_step_that_is_silent_after_the_retry()
    {
        ManualClock clock = new(Noon);
        JobScheduler scheduler = new(clock, Deadline);
        scheduler.Schedule(Silent("transcode"));
        scheduler.Start();

        JobSupervisor supervisor = new(scheduler, clock);
        clock.Advance(TimeSpan.FromMinutes(6));
        supervisor.Sweep();
        clock.Advance(TimeSpan.FromMinutes(6));
        supervisor.Sweep();

        // Retried once, still nothing. Retrying for ever is how a stalled step
        // becomes a permanent load nobody is looking at.
        Assert.Equal(JobStatus.Escalated, scheduler.StatusOf("transcode"));
        Assert.Equal(1, supervisor.Retries);
    }

    [Fact]
    public void Does_not_retry_a_step_that_answered_in_time()
    {
        ManualClock clock = new(Noon);
        JobScheduler scheduler = new(clock, Deadline);
        RemoteAgent agent = Answers("probe");
        scheduler.Schedule(agent);
        scheduler.Start();

        JobSupervisor supervisor = new(scheduler, clock);
        clock.Advance(TimeSpan.FromHours(1));
        supervisor.Sweep();

        // An hour later and still no retry: the step is done, and a supervisor
        // that re-ran completed work would be duplicating a transcode.
        Assert.Equal(1, agent.Attempts);
        Assert.Equal(0, supervisor.Retries);
    }

    [Fact]
    public void Reports_which_step_stalled()
    {
        ManualClock clock = new(Noon);
        JobScheduler scheduler = new(clock, Deadline);
        scheduler.Schedule(Answers("probe"));
        scheduler.Schedule(Silent("transcode"));
        scheduler.Schedule(Answers("publish"));
        scheduler.Start();

        JobSupervisor supervisor = new(scheduler, clock);
        clock.Advance(TimeSpan.FromMinutes(6));
        supervisor.Sweep();
        clock.Advance(TimeSpan.FromMinutes(6));
        supervisor.Sweep();

        // Naming the step is the whole value of the escalation: an operator
        // needs to know which one, not that something somewhere stalled.
        Assert.Equal(["transcode"], supervisor.Escalated);
    }
}
