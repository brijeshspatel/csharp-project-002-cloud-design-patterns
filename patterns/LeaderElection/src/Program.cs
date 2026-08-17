using LeaderElection;

// Three instances of the same application, one nightly billing run. It must
// happen once, and no instance knows which of them is special.

Console.WriteLine("Leader Election - three instances, one billing run");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

ManualClock clock = new(new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero));
LeaseStore store = new(clock, duration: TimeSpan.FromMinutes(5));

ElectionParticipant[] instances =
[
    new("instance-1", store),
    new("instance-2", store),
    new("instance-3", store),
];

int billingRuns = 0;

Console.WriteLine("00:00 - all three wake up and contend");
Console.WriteLine(new string('-', 60));
foreach (ElectionParticipant instance in instances)
{
    bool won = instance.TryBecomeLeader();
    bool ran = instance.RunExclusiveWork(() => billingRuns++);
    Console.WriteLine($"  {instance.Id}: leader={won,-5} billed={ran}");
}

Console.WriteLine($"  billing runs: {billingRuns}");
Console.WriteLine($"  holder: {store.Holder}");

Console.WriteLine();
clock.Advance(TimeSpan.FromMinutes(3));
Console.WriteLine($"{clock.UtcNow:HH:mm} - the leader renews, so nothing changes");
Console.WriteLine(new string('-', 60));
instances[0].RenewLease();
Console.WriteLine($"  instance-1 renewed; holder: {store.Holder}");
Console.WriteLine($"  instance-2 attempts: {instances[1].TryBecomeLeader()}");

Console.WriteLine();
clock.Advance(TimeSpan.FromMinutes(6));
Console.WriteLine($"{clock.UtcNow:HH:mm} - the leader was killed, and could not say so");
Console.WriteLine(new string('-', 60));
Console.WriteLine("  no shutdown, no handover, no announcement - it is simply gone");
Console.WriteLine($"  holder now: {store.Holder ?? "nobody - the lease lapsed"}");
Console.WriteLine($"  instance-1 still believes it leads? {instances[0].IsLeader}");

Console.WriteLine();
Console.WriteLine($"{clock.UtcNow:HH:mm} - a successor takes over");
Console.WriteLine(new string('-', 60));
Console.WriteLine($"  instance-2 attempts: {instances[1].TryBecomeLeader()}");
Console.WriteLine($"  holder: {store.Holder}");

Console.WriteLine();
clock.Advance(TimeSpan.FromMinutes(1));
Console.WriteLine($"{clock.UtcNow:HH:mm} - a clean shutdown hands over at once");
Console.WriteLine(new string('-', 60));
instances[1].StepDown();
Console.WriteLine($"  instance-2 stepped down; holder: {store.Holder ?? "nobody"}");
Console.WriteLine($"  instance-3 attempts: {instances[2].TryBecomeLeader()}");
Console.WriteLine($"  holder: {store.Holder}");

Console.WriteLine();
Console.WriteLine($"Billing ran {billingRuns} time across the whole cluster.");
Console.WriteLine();
Console.WriteLine("The expiry is what makes this safe. A crashed leader cannot");
Console.WriteLine("announce that it crashed, so leadership must end by itself.");
