using SequentialConvoy;

// Ledger entries for two accounts, arriving interleaved and out of order.
// Account A is missing an entry and cannot proceed; account B is untouched by
// that, which is the point of the pattern.

Console.WriteLine("Sequential Convoy - order within a group, groups independent");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

ConvoyDispatcher dispatcher = new();

ConvoyMessage[] arrivals =
[
    new("ACC-A", 2, "ACC-A #2  withdraw 20.00"),
    new("ACC-B", 1, "ACC-B #1  deposit  50.00"),
    new("ACC-A", 3, "ACC-A #3  deposit  15.00"),
    new("ACC-B", 2, "ACC-B #2  withdraw 10.00"),
];

Console.WriteLine("Arrivals, in the order the broker delivered them");
Console.WriteLine(new string('-', 62));

foreach (ConvoyMessage message in arrivals)
{
    dispatcher.Accept(message);
    Console.WriteLine($"  received {message.Group} #{message.Sequence}" +
                      $"  -> released so far: {dispatcher.Released.Count}, " +
                      $"held: {dispatcher.HeldCount}");
}

Console.WriteLine();
Console.WriteLine($"ACC-A is holding {dispatcher.HeldFor("ACC-A")} entries, waiting for #1.");
Console.WriteLine($"ACC-B has released {dispatcher.Released.Count(m => m.Group == "ACC-B")}" +
                  " entries and was never blocked by it.");

Console.WriteLine();
Console.WriteLine("The missing entry finally arrives");
Console.WriteLine(new string('-', 62));

dispatcher.Accept(new ConvoyMessage("ACC-A", 1, "ACC-A #1  deposit 100.00"));

Console.WriteLine("  received ACC-A #1 - the gap closes and the convoy moves");
Console.WriteLine();
Console.WriteLine("Delivered, in order");
Console.WriteLine(new string('-', 62));

foreach (ConvoyMessage message in dispatcher.Released)
{
    Console.WriteLine($"  {message.Payload}");
}

Console.WriteLine();
Console.WriteLine("ACC-A's entries are in sequence despite arriving 2, 3, 1.");
Console.WriteLine("A single ordered queue would have given the same guarantee");
Console.WriteLine("and stalled ACC-B behind ACC-A's gap for the whole time.");
