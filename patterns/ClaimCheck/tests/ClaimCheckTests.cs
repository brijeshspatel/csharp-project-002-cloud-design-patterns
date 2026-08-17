namespace ClaimCheck.Tests;

/// <summary>
/// What the claim check guarantees: that a payload too large for the bus is
/// still delivered, that the receiver gets exactly what the sender sent, and
/// that a small payload does not pay for the store it does not need.
///
/// The first test is the one that makes the rest meaningful. Without a bus that
/// genuinely refuses, this pattern is indirection whose purpose is invisible.
/// </summary>
public class ClaimCheckTests
{
    private const int BusLimit = 64;

    private static string Payload(int bytes) => new('x', bytes);

    private static (MessageBus Bus, InMemoryPayloadStore Store) Build() =>
        (new MessageBus(BusLimit), new InMemoryPayloadStore());

    [Fact]
    public void Refuses_a_payload_larger_than_the_bus_allows()
    {
        (MessageBus bus, _) = Build();

        // The constraint is real, and everything else here exists because of
        // it. A bus that accepted anything would make the pattern pointless.
        MessageTooLargeException refusal = Assert.Throws<MessageTooLargeException>(
            () => bus.Send(new BusMessage("scan", Payload(200))));

        Assert.Contains("200", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(0, bus.Depth);
    }

    [Fact]
    public void Sends_a_large_payload_as_a_claim_check()
    {
        (MessageBus bus, InMemoryPayloadStore store) = Build();
        ClaimCheckSender sender = new(bus, store, inlineLimit: BusLimit / 2);

        sender.Send("scan", Payload(200));

        // What travelled on the bus is a token, not the document.
        BusMessage onTheWire = Assert.Single(bus.Peek());
        Assert.StartsWith(ClaimCheckToken.Prefix, onTheWire.Body, StringComparison.Ordinal);
        Assert.True(onTheWire.SizeInBytes <= BusLimit);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Retrieves_the_original_payload_from_the_check()
    {
        (MessageBus bus, InMemoryPayloadStore store) = Build();
        ClaimCheckSender sender = new(bus, store, inlineLimit: BusLimit / 2);
        ClaimCheckReceiver receiver = new(bus, store);

        string original = Payload(200);
        sender.Send("scan", original);

        Assert.True(receiver.TryReceive(out string received));
        Assert.Equal(original, received);
    }

    [Fact]
    public void Sends_a_small_payload_without_using_the_store()
    {
        (MessageBus bus, InMemoryPayloadStore store) = Build();
        ClaimCheckSender sender = new(bus, store, inlineLimit: BusLimit / 2);
        ClaimCheckReceiver receiver = new(bus, store);

        sender.Send("note", "short");

        // A round trip to the store costs latency and money. Below the
        // threshold the payload rides inline and the store is untouched.
        Assert.Equal(0, store.Count);
        Assert.True(receiver.TryReceive(out string received));
        Assert.Equal("short", received);
    }

    [Fact]
    public void Fails_clearly_when_the_payload_has_already_been_collected()
    {
        (MessageBus bus, InMemoryPayloadStore store) = Build();
        ClaimCheckSender sender = new(bus, store, inlineLimit: BusLimit / 2);
        ClaimCheckReceiver receiver = new(bus, store);

        sender.Send("scan", Payload(200));

        // Lifetimes are independent: the store may expire a payload while its
        // message is still queued. Failing clearly beats returning an empty
        // document that looks like a successful read.
        store.CollectAll();

        Assert.Throws<PayloadUnavailableException>(() => receiver.TryReceive(out _));
    }

    [Fact]
    public void Leaves_the_message_on_the_bus_when_redemption_fails()
    {
        (MessageBus bus, InMemoryPayloadStore store) = Build();
        ClaimCheckSender sender = new(bus, store, inlineLimit: BusLimit / 2);
        ClaimCheckReceiver receiver = new(bus, store);

        sender.Send("scan", Payload(200));
        store.CollectAll();

        Assert.Throws<PayloadUnavailableException>(() => receiver.TryReceive(out _));

        // The receiver redeems before it consumes. Dequeuing first would
        // destroy the message on failure - and the evidence with it, leaving
        // nothing to retry, inspect or dead-letter.
        Assert.Equal(1, bus.Depth);
    }
}
