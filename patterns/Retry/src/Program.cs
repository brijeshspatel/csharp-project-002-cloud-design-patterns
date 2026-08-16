using Retry;

// Two scenarios, because a retry policy has two outcomes and showing only the
// happy one teaches half the pattern. The delays are simulated: RecordingTimeSource
// returns immediately and records what it was asked to wait for, so this runs in
// milliseconds and prints the schedule it would have followed.

Console.WriteLine("Retry - handling a dependency that fails transiently");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

await RecoversAsync().ConfigureAwait(false);
Console.WriteLine();
await GivesUpAsync().ConfigureAwait(false);

Console.WriteLine();
Console.WriteLine("Both paths shown. No delay above was really waited for -");
Console.WriteLine("see the 'In Azure' section of this pattern's README.");

static async Task RecoversAsync()
{
    Console.WriteLine("Scenario 1: the dependency recovers on the third call");
    Console.WriteLine(new string('-', 62));

    RecordingTimeSource time = new();
    FlakyService service = new(failuresBeforeSuccess: 2);
    RetryPolicy policy = new(
        maxAttempts: 4,
        baseDelay: TimeSpan.FromMilliseconds(100),
        maxDelay: TimeSpan.FromSeconds(2),
        time: time,
        jitter: () => 1.0);

    string result = await policy.ExecuteAsync(async attempt =>
    {
        Console.WriteLine($"  attempt {attempt}: calling the service");
        try
        {
            string value = await service.CallAsync().ConfigureAwait(false);
            Console.WriteLine($"  attempt {attempt}: succeeded, returned '{value}'");
            return value;
        }
        catch (TransientFailureException failure)
        {
            Console.WriteLine($"  attempt {attempt}: failed - {failure.Message}");
            throw;
        }
    }).ConfigureAwait(false);

    Console.WriteLine($"  result: '{result}' after {service.Calls} calls");
    Report(time);
}

static async Task GivesUpAsync()
{
    Console.WriteLine("Scenario 2: the dependency never recovers");
    Console.WriteLine(new string('-', 62));

    RecordingTimeSource time = new();
    BrokenService service = new();
    RetryPolicy policy = new(
        maxAttempts: 4,
        baseDelay: TimeSpan.FromMilliseconds(100),
        maxDelay: TimeSpan.FromSeconds(2),
        time: time,
        jitter: () => 1.0);

    try
    {
        await policy.ExecuteAsync<string>(attempt =>
        {
            Console.WriteLine($"  attempt {attempt}: calling the service");
            return service.CallAsync();
        }).ConfigureAwait(false);
    }
    catch (TransientFailureException failure)
    {
        Console.WriteLine($"  gave up after {service.Calls} attempts");
        Console.WriteLine($"  the last failure propagated unwrapped: {failure.Message}");
    }

    Report(time);
}

static void Report(RecordingTimeSource time)
{
    if (time.Delays.Count == 0)
    {
        Console.WriteLine("  no delays were needed");
        return;
    }

    string schedule = string.Join(
        ", ",
        time.Delays.Select(delay => $"{delay.TotalMilliseconds:0}ms"));
    Console.WriteLine($"  delay schedule (simulated): {schedule}");
}
