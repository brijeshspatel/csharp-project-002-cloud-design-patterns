using ComputeResourceConsolidation;

// Five small scheduled tasks that each had a host of their own, moved onto one
// - and then a sixth that does not fit.

Console.WriteLine("Compute Resource Consolidation - fewer units, shared fate");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

ConsolidatedHost host = new(capacity: 100);
host.Add(new ScheduledTask("nightly-report", cost: 10));
host.Add(new ScheduledTask("cache-warm", cost: 10));
host.Add(new ScheduledTask("index-rebuild", cost: 10));
host.Add(new ScheduledTask("licence-sweep", cost: 10));
host.Add(new ScheduledTask("audit-export", cost: 10));

Console.WriteLine("Before: one host each");
Console.WriteLine(new string('-', 62));
Console.WriteLine($"  tasks:              {host.Tasks.Count}");
Console.WriteLine($"  computational units: {host.Usage.UnitsBefore}");
Console.WriteLine("  Each idle for most of the day, and paid for around the clock.");

Console.WriteLine();
Console.WriteLine("After: one host between them");
Console.WriteLine(new string('-', 62));
Console.WriteLine($"  computational units: {host.Usage.UnitsAfter}");
Console.WriteLine($"  units removed:       {host.Usage.UnitsRemoved}");
Console.WriteLine($"  utilisation:         {host.Usage.Utilisation}%");
Console.WriteLine();
foreach (string result in host.RunAll())
{
    Console.WriteLine($"    {result}");
}

Console.WriteLine();
Console.WriteLine("  Four units that no longer have to exist, be patched, or be paid");
Console.WriteLine("  for - and the tasks are unchanged by the move.");

Console.WriteLine();
Console.WriteLine("A sixth task arrives, and it is not small");
Console.WriteLine(new string('-', 62));

host.Add(new ScheduledTask("bulk-import", cost: 60));

Console.WriteLine($"  utilisation:      {host.Usage.Utilisation}%");
Console.WriteLine($"  over-committed:   {host.IsOverCommitted}");
Console.WriteLine();
foreach (string result in host.RunAll())
{
    Console.WriteLine($"    {result}");
}

Console.WriteLine();
Console.WriteLine("  Nothing failed and nothing was dropped. Everything got slower,");
Console.WriteLine("  including the four tasks that had nothing to do with the import.");
Console.WriteLine("  Shared capacity means shared fate, and that is the price of the");
Console.WriteLine("  units removed above.");

Console.WriteLine();
Console.WriteLine("This pattern argues the opposite of its tier-mates: Deployment");
Console.WriteLine("Stamps and Geode both say deploy more copies. They answer different");
Console.WriteLine("questions - idle capacity paid for, against how much one failure");
Console.WriteLine("takes with it - and a system can sensibly do both.");
