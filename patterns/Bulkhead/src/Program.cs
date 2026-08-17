using Bulkhead;

// One host serving checkout and reporting. Reporting is slow and popular, and
// without isolation it would consume every worker in the process. Here it
// exhausts its own pool and checkout carries on.

Console.WriteLine("Bulkhead - one compartment floods, the ship stays up");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

using BulkheadPolicy bulkhead = new(new Dictionary<string, int>
{
    ["checkout"] = 2,
    ["reporting"] = 1,
});

Console.WriteLine("Reporting fills its pool and is refused; checkout is untouched");
Console.WriteLine(new string('-', 62));

// Held deliberately, rather than raced: this is the exhausted state, created.
using IDisposable heldReportingSlot = bulkhead.AcquireSlot("reporting");
Console.WriteLine("  reporting: slot 1 of 1 taken and held by a long-running report");

await TryAsync("reporting", "second report").ConfigureAwait(false);
await TryAsync("checkout", "order A").ConfigureAwait(false);
await TryAsync("checkout", "order B").ConfigureAwait(false);

Console.WriteLine();
Console.WriteLine("A failure inside a partition gives its slot back");
Console.WriteLine(new string('-', 62));

try
{
    await bulkhead.ExecuteAsync<string>(
        "checkout", () => throw new InvalidOperationException("payment declined"))
        .ConfigureAwait(false);
}
catch (InvalidOperationException failure)
{
    Console.WriteLine($"  checkout:  failed - {failure.Message}");
}

await TryAsync("checkout", "order C").ConfigureAwait(false);

Console.WriteLine();
Console.WriteLine("A partition nobody configured is refused, not admitted");
Console.WriteLine(new string('-', 62));
await TryAsync("serch", "typo in the partition name").ConfigureAwait(false);

Console.WriteLine();
Console.WriteLine("Reporting never touched checkout's capacity. That isolation");
Console.WriteLine("is the entire pattern.");

async Task TryAsync(string partition, string work)
{
    try
    {
        string result = await bulkhead
            .ExecuteAsync(partition, () => Task.FromResult($"{work} completed"))
            .ConfigureAwait(false);
        Console.WriteLine($"  {partition,-10} {result}");
    }
    catch (BulkheadRejectedException rejected)
    {
        Console.WriteLine($"  {partition,-10} rejected - {rejected.Message}");
    }
}
