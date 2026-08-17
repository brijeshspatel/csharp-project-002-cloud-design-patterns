using Saga;

// An order across three services, where the coordinator is lost mid-run and a
// replacement finishes the job from the log alone.

Console.WriteLine("Saga - the log is what survives the coordinator");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

SagaLog log = new();
List<string> world = [];

bool Perform(string step)
{
    world.Add(step);
    Console.WriteLine($"    performed {step}");
    return true;
}

bool Compensate(string step)
{
    world.Remove(step);
    Console.WriteLine($"    countered {step}");
    return true;
}

Console.WriteLine("A coordinator starts, and is lost after inventory");
Console.WriteLine(new string('-', 60));

OrderSaga lost = new(log, Perform, Compensate);
lost.RunUpTo("inventory");

Console.WriteLine($"  the world now holds: {string.Join(", ", world)}");
Console.WriteLine("  the coordinator is gone; its stack and its intentions with it");

Console.WriteLine();
Console.WriteLine("The log, which is all that survives");
Console.WriteLine(new string('-', 60));
foreach (SagaEntry entry in log.Entries)
{
    Console.WriteLine($"  {entry.Step,-10} {entry.Status}");
}

Console.WriteLine();
Console.WriteLine("A replacement reads the log and finishes");
Console.WriteLine(new string('-', 60));

OrderSaga replacement = new(log, Perform, Compensate);
SagaStatus status = replacement.Run();

Console.WriteLine($"  result: {status}");
Console.WriteLine($"  the world now holds: {string.Join(", ", world)}");
Console.WriteLine("  payment and inventory were not repeated: the log said they were done");

Console.WriteLine();
Console.WriteLine("The same operation, where shipping fails");
Console.WriteLine(new string('-', 60));

SagaLog failing = new();
List<string> other = [];
OrderSaga doomed = new(
    failing,
    step =>
    {
        // No courier capacity. The step changed nothing, so nothing counters it.
        if (step == "shipping")
        {
            Console.WriteLine($"    FAILED    {step}");
            return false;
        }

        other.Add(step);
        Console.WriteLine($"    performed {step}");
        return true;
    },
    step => { other.Remove(step); Console.WriteLine($"    countered {step}"); return true; });

SagaStatus outcome = doomed.Run();

Console.WriteLine($"  result: {outcome}");
Console.WriteLine($"  the world now holds: {(other.Count == 0 ? "nothing" : string.Join(", ", other))}");

Console.WriteLine();
Console.WriteLine("Compensation ran last-first, and the log records every step of it.");
Console.WriteLine("Written at the end instead, the log would describe only the sagas");
Console.WriteLine("that never needed it.");
