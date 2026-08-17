using QueueBasedLoadLeveling;

// A ticket sale opens. Twenty orders arrive at once at a service that can take
// three per cycle. The unbuffered path is shown first, because the rejections
// are what the queue exists to prevent.

Console.WriteLine("Queue-Based Load Leveling - absorbing a spike a service cannot take");
Console.WriteLine(new string('=', 68));
Console.WriteLine();

const int BurstSize = 20;
const int ServiceCapacity = 3;

Console.WriteLine("Without a queue: the burst hits the service directly");
Console.WriteLine(new string('-', 68));

OrderService direct = new(perCycleCapacity: ServiceCapacity);
int rejected = 0;
for (int i = 1; i <= BurstSize; i++)
{
    try
    {
        direct.Accept(new Order($"ORD-{i:000}"));
    }
    catch (ServiceOverloadedException)
    {
        rejected++;
    }
}

Console.WriteLine($"  accepted {direct.Accepted.Count} of {BurstSize}");
Console.WriteLine($"  rejected {rejected} - those customers saw an error");

Console.WriteLine();
Console.WriteLine("With a queue: the same burst, served over several cycles");
Console.WriteLine(new string('-', 68));

LevellingBuffer queue = new(capacity: 32);
OrderService buffered = new(perCycleCapacity: ServiceCapacity);

int refusedByQueue = 0;
for (int i = 1; i <= BurstSize; i++)
{
    if (!queue.TryEnqueue(new Order($"ORD-{i:000}")))
    {
        refusedByQueue++;
    }
}

Console.WriteLine($"  buffered {queue.Count} orders, {refusedByQueue} refused by the queue");

int cycle = 0;
while (queue.Count > 0)
{
    cycle++;
    buffered.BeginCycle();
    int drained = queue.DrainInto(buffered);
    Console.WriteLine($"  cycle {cycle}: handed the service {drained}, {queue.Count} still waiting");
}

Console.WriteLine($"  accepted {buffered.Accepted.Count} of {BurstSize}, none rejected");

Console.WriteLine();
Console.WriteLine("The queue is bounded at 32. That refusal count is the honest");
Console.WriteLine("signal an unbounded buffer would have hidden until it ran out");
Console.WriteLine("of memory instead.");
