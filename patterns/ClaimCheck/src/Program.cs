using ClaimCheck;

// A scanned document is too large for the bus. The direct send is shown failing
// first, because without that refusal the claim check looks like indirection
// for its own sake.

Console.WriteLine("Claim Check - carrying a payload the bus will not take");
Console.WriteLine(new string('=', 58));
Console.WriteLine();

const int BusLimit = 256;
MessageBus bus = new(BusLimit);
InMemoryPayloadStore store = new();

string scan = new('x', 4096);
Console.WriteLine($"The bus accepts {BusLimit} bytes. The scan is {InMemoryPayloadStore.SizeOf(scan)}.");
Console.WriteLine();

Console.WriteLine("Sending it directly");
Console.WriteLine(new string('-', 58));
try
{
    bus.Send(new BusMessage("scan", scan));
}
catch (MessageTooLargeException refused)
{
    Console.WriteLine($"  refused: {refused.Message}");
}

Console.WriteLine();
Console.WriteLine("Sending it as a claim check");
Console.WriteLine(new string('-', 58));

ClaimCheckSender sender = new(bus, store, inlineLimit: 128);
ClaimCheckReceiver receiver = new(bus, store);

sender.Send("scan", scan);

BusMessage onTheWire = bus.Peek().First();
Console.WriteLine($"  on the bus: '{onTheWire.Body}' ({onTheWire.SizeInBytes} bytes)");
Console.WriteLine($"  in the store: {store.Count} payload");

receiver.TryReceive(out string received);
Console.WriteLine($"  received {InMemoryPayloadStore.SizeOf(received)} bytes, " +
                  $"identical to what was sent: {received == scan}");

Console.WriteLine();
Console.WriteLine("A short note does not pay for the store");
Console.WriteLine(new string('-', 58));

sender.Send("note", "approved by the duty manager");
Console.WriteLine($"  in the store: {store.Count} payload (unchanged)");
receiver.TryReceive(out string note);
Console.WriteLine($"  received inline: '{note}'");

Console.WriteLine();
Console.WriteLine("The hazard: the payload can expire while the message is queued");
Console.WriteLine(new string('-', 58));

sender.Send("scan", scan);
store.CollectAll();
try
{
    receiver.TryReceive(out _);
}
catch (PayloadUnavailableException gone)
{
    Console.WriteLine($"  {gone.Message}");
    Console.WriteLine("  failing clearly beats returning an empty document that");
    Console.WriteLine("  looks like a successful read");
}
