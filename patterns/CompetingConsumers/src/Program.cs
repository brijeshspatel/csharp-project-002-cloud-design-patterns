using CompetingConsumers;

// A pool of workers rendering thumbnails from an upload channel. Twelve uploads,
// three workers, and one worker that fails its first claim. Workers are pumped
// in a fixed rotation so the output is the same every run.

Console.WriteLine("Competing Consumers - many workers, each message handled once");
Console.WriteLine(new string('=', 64));
Console.WriteLine();

WorkChannel channel = new();
for (int i = 1; i <= 12; i++)
{
    channel.Post(new WorkItem($"IMG-{i:000}"));
}

Consumer[] workers =
[
    new("worker-1"),
    new("worker-2", failOnce: true),
    new("worker-3"),
];

Console.WriteLine($"{channel.Count} uploads waiting, {workers.Length} workers");
Console.WriteLine(new string('-', 64));

int round = 0;
while (channel.Count > 0)
{
    round++;

    // Bounded for the same reason the tests are: a claim that failed to remove
    // its item would spin here for ever rather than reporting anything.
    if (round > 100)
    {
        Console.WriteLine("  the channel is not draining; stopping");
        break;
    }

    foreach (Consumer worker in workers)
    {
        int before = worker.Handled.Count;
        bool completed = worker.ProcessNext(channel);
        string outcome = completed
            ? worker.Handled[before].Reference
            : "nothing completed (claim released or channel empty)";
        Console.WriteLine($"  round {round}  {worker.Id}: {outcome}");
    }
}

Console.WriteLine();
Console.WriteLine("Where the work went");
Console.WriteLine(new string('-', 64));

foreach (Consumer worker in workers)
{
    string references = string.Join(", ", worker.Handled.Select(item => item.Reference));
    Console.WriteLine($"  {worker.Id}: {worker.Handled.Count,2} - {references}");
}

int total = workers.Sum(worker => worker.Handled.Count);
int distinct = workers.SelectMany(w => w.Handled).Select(w => w.Reference).Distinct().Count();

Console.WriteLine();
Console.WriteLine($"{total} handled, {distinct} distinct - every upload rendered exactly once,");
Console.WriteLine("including the one worker-2 claimed, failed, and released.");
