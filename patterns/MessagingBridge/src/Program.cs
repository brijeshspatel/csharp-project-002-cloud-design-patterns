using MessagingBridge;

// A migration in progress. The old on-premises queue still carries orders; the
// new typed bus is where everything is heading. The bridge lets both run at
// once, which is what makes the migration incremental rather than a cutover.

Console.WriteLine("Messaging Bridge - two systems that cannot speak to each other");
Console.WriteLine(new string('=', 64));
Console.WriteLine();

LegacyBroker legacy = new();
ModernBus modern = new();
BridgeService bridge = new(legacy, modern);

Console.WriteLine("Legacy to modern");
Console.WriteLine(new string('-', 64));

legacy.Put("ORDER.PLACED|ORD-001");
legacy.Put("ORDER.PLACED|ORD-002");
Console.WriteLine($"  legacy queue holds {legacy.Depth}");

while (bridge.PumpLegacyToModern() == 1)
{
    // Each pump carries one message across.
}

Console.WriteLine($"  after pumping: legacy {legacy.Depth}, modern {modern.Depth}");

modern.TryReceive(out Envelope crossed);
Console.WriteLine($"  first envelope: subject '{crossed.Subject}', body '{crossed.Body}'");
Console.WriteLine($"    headers: source={crossed.Headers["source"]}, " +
                  $"schema-version={crossed.Headers["schema-version"]}");
Console.WriteLine("    the legacy queue had no headers; the bridge supplied them");

// Drain what is left, so the next phase demonstrably moves the envelope it
// publishes rather than a leftover from this one.
while (modern.TryReceive(out _))
{
}

Console.WriteLine();
Console.WriteLine("Modern to legacy");
Console.WriteLine(new string('-', 64));

modern.Publish(new Envelope(
    "ORDER.SHIPPED",
    "ORD-003",
    new Dictionary<string, string> { ["source"] = "modern-bus", ["trace-id"] = "abc123" }));

bridge.PumpModernToLegacy();
legacy.TryTake(out string backOnLegacy);
Console.WriteLine($"  on the legacy queue: '{backOnLegacy}'");
Console.WriteLine("    the trace-id was dropped - the legacy queue has nowhere to put it");

Console.WriteLine();
Console.WriteLine("A message the bridge cannot translate");
Console.WriteLine(new string('-', 64));

legacy.Put("this is not in the expected format");
int carried = bridge.PumpLegacyToModern();

Console.WriteLine($"  pumped {carried} messages");
Console.WriteLine($"  legacy queue still holds {legacy.Depth} - it was left, not consumed");
Console.WriteLine("  destroying it would leave nothing to inspect or replay");
