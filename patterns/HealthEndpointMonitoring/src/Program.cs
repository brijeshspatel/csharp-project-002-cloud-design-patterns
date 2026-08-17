using HealthEndpointMonitoring;

// A web application aggregating three dependency probes. Three scenarios, so
// all three statuses are visible - including the one that matters most, where
// the check itself is broken and the endpoint survives it.

Console.WriteLine("Health Endpoint Monitoring - reporting the worst thing you found");
Console.WriteLine(new string('=', 66));
Console.WriteLine();

TimeSpan slowThreshold = TimeSpan.FromMilliseconds(500);

Probe("Everything is fine", clock =>
[
    new DatabaseCheck(clock, TimeSpan.FromMilliseconds(20)),
    new CacheCheck(HealthStatus.Healthy),
]);

Probe("The database is answering, but slowly", clock =>
[
    new DatabaseCheck(clock, TimeSpan.FromMilliseconds(900)),
    new CacheCheck(HealthStatus.Healthy),
]);

Probe("A check is itself broken", clock =>
[
    new DatabaseCheck(clock, TimeSpan.FromMilliseconds(20)),
    new CacheCheck(HealthStatus.Degraded),
    new BrokenBrokerCheck(),
]);

Console.WriteLine("The third probe is the one worth reading: the broker check threw,");
Console.WriteLine("and the endpoint reported it instead of failing with it.");

void Probe(string title, Func<ManualClock, IHealthCheck[]> build)
{
    Console.WriteLine(title);
    Console.WriteLine(new string('-', 66));

    ManualClock clock = new(new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero));
    HealthEndpoint endpoint = new(build(clock), slowThreshold, clock);
    HealthReport report = endpoint.ProbeAsync().GetAwaiter().GetResult();

    Console.WriteLine($"  overall: {report.Status}");
    foreach (HealthEntry entry in report.Entries)
    {
        Console.WriteLine(
            $"    {entry.Name,-12} {entry.Status,-9} " +
            $"{entry.Duration.TotalMilliseconds,4:0}ms  {entry.Detail}");
    }

    Console.WriteLine();
}
