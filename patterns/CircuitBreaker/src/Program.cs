using CircuitBreaker;

// A checkout service calling a remote inventory service that goes down and later
// recovers. The clock is moved by hand, so the thirty-second break passes
// instantly and the demonstration finishes in milliseconds.

Console.WriteLine("Circuit Breaker - cutting off a dependency that is failing");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

ManualClock clock = new(new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero));
FlakyInventoryService inventory = new(failuresBeforeRecovery: 3);
CircuitBreakerPolicy breaker = new(
    failureThreshold: 3,
    breakDuration: TimeSpan.FromSeconds(30),
    clock);

Console.WriteLine("Phase 1: the dependency starts failing");
Console.WriteLine(new string('-', 62));
for (int attempt = 1; attempt <= 3; attempt++)
{
    await AttemptAsync($"call {attempt}").ConfigureAwait(false);
}

Console.WriteLine();
Console.WriteLine("Phase 2: the circuit is open, so calls are refused immediately");
Console.WriteLine(new string('-', 62));
await AttemptAsync("call 4").ConfigureAwait(false);
await AttemptAsync("call 5").ConfigureAwait(false);
Console.WriteLine($"  the dependency has been called {inventory.Calls} times in total,");
Console.WriteLine("  so the last two calls never reached it");

Console.WriteLine();
Console.WriteLine("Phase 3: the break duration elapses and one probe is allowed");
Console.WriteLine(new string('-', 62));
clock.Advance(TimeSpan.FromSeconds(30));
Console.WriteLine($"  clock advanced 30s, state is now {breaker.State}");
await AttemptAsync("probe").ConfigureAwait(false);
Console.WriteLine($"  state is now {breaker.State}");

Console.WriteLine();
Console.WriteLine("No wait above was real: the clock is advanced by hand.");
Console.WriteLine("See the 'In Azure' section of this pattern's README.");

async Task AttemptAsync(string label)
{
    try
    {
        string stock = await breaker
            .ExecuteAsync(() => inventory.CheckStockAsync("SKU-4471"))
            .ConfigureAwait(false);
        Console.WriteLine($"  {label}: {stock} [state {breaker.State}]");
    }
    catch (CircuitOpenException)
    {
        Console.WriteLine($"  {label}: refused without calling the service [state {breaker.State}]");
    }
    catch (DependencyFailureException failure)
    {
        Console.WriteLine($"  {label}: {failure.Message} [state {breaker.State}]");
    }
}
