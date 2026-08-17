using SchedulerAgentSupervisor;

// A transcoding job in three steps. One agent accepts the work and is never
// heard from again - which is the case this pattern exists for.

Console.WriteLine("Scheduler Agent Supervisor - noticing the step that never answered");
Console.WriteLine(new string('=', 66));
Console.WriteLine();

ManualClock clock = new(new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero));
JobScheduler scheduler = new(clock, timeout: TimeSpan.FromMinutes(5));

scheduler.Schedule(new RemoteAgent("probe", _ => true));
scheduler.Schedule(new RemoteAgent("transcode", _ => false));
scheduler.Schedule(new RemoteAgent("publish", _ => true));

JobSupervisor supervisor = new(scheduler, clock);

Console.WriteLine($"The scheduler asks each agent once, at {clock.UtcNow:HH:mm}");
Console.WriteLine(new string('-', 66));
scheduler.Start();
Report();

Console.WriteLine();
Console.WriteLine("The scheduler does not wait. It recorded a deadline instead:");
Console.WriteLine($"  transcode is due to answer by {scheduler.DeadlineOf("transcode"):HH:mm}");

Console.WriteLine();
Console.WriteLine("12:04 - the supervisor sweeps, and does nothing");
Console.WriteLine(new string('-', 66));
clock.Advance(TimeSpan.FromMinutes(4));
supervisor.Sweep();
Console.WriteLine($"  retries so far: {supervisor.Retries}");
Console.WriteLine("  four minutes of silence is slow, not stalled. A supervisor that");
Console.WriteLine("  acted here would duplicate work that was going to finish.");

Console.WriteLine();
Console.WriteLine("12:06 - the deadline has passed, so it retries");
Console.WriteLine(new string('-', 66));
clock.Advance(TimeSpan.FromMinutes(2));
supervisor.Sweep();
Console.WriteLine($"  retries so far: {supervisor.Retries}");
Report();

Console.WriteLine();
Console.WriteLine("12:12 - still nothing, so it escalates");
Console.WriteLine(new string('-', 66));
clock.Advance(TimeSpan.FromMinutes(6));
supervisor.Sweep();
Console.WriteLine($"  retries so far: {supervisor.Retries}");
Console.WriteLine($"  escalated:      {string.Join(", ", supervisor.Escalated)}");
Report();

Console.WriteLine();
Console.WriteLine("It retried once and stopped. Retrying for ever turns a stalled step");
Console.WriteLine("into permanent background load that never asks for attention - which");
Console.WriteLine("is worse than a failure, because a failure at least gets reported.");

void Report()
{
    foreach (string step in scheduler.Steps)
    {
        Console.WriteLine($"    {step,-12} {scheduler.StatusOf(step)}");
    }
}
