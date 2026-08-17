namespace MessagingBridge.Tests;

/// <summary>
/// What a bridge guarantees: that a message crosses between two systems that
/// cannot speak to each other, that its payload survives the crossing intact,
/// and that anything it cannot translate is **left where it was** rather than
/// consumed and lost.
/// </summary>
public class BridgeServiceTests
{
    private static (LegacyBroker Legacy, ModernBus Modern, BridgeService Bridge) Build()
    {
        LegacyBroker legacy = new();
        ModernBus modern = new();
        return (legacy, modern, new BridgeService(legacy, modern));
    }

    [Fact]
    public void Carries_a_message_from_the_legacy_queue_to_the_modern_bus()
    {
        (LegacyBroker legacy, ModernBus modern, BridgeService bridge) = Build();
        legacy.Put("ORDER.PLACED|ORD-001");

        Assert.Equal(1, bridge.PumpLegacyToModern());

        Assert.Equal(0, legacy.Depth);
        Assert.Equal(1, modern.Depth);
    }

    [Fact]
    public void Carries_a_message_from_the_modern_bus_to_the_legacy_queue()
    {
        (LegacyBroker legacy, ModernBus modern, BridgeService bridge) = Build();
        modern.Publish(new Envelope("ORDER.SHIPPED", "ORD-002", new Dictionary<string, string>()));

        Assert.Equal(1, bridge.PumpModernToLegacy());

        Assert.Equal(1, legacy.Depth);
        Assert.Equal(0, modern.Depth);
    }

    [Fact]
    public void Preserves_the_payload_across_translation()
    {
        (LegacyBroker legacy, ModernBus modern, BridgeService bridge) = Build();
        legacy.Put("ORDER.PLACED|ORD-001");

        bridge.PumpLegacyToModern();
        Assert.True(modern.TryReceive(out Envelope crossed));

        Assert.Equal("ORDER.PLACED", crossed.Subject);
        Assert.Equal("ORD-001", crossed.Body);

        // And back again, unchanged. A bridge that mangles a payload on a round
        // trip is worse than no bridge, because the corruption is silent.
        modern.Publish(crossed);
        bridge.PumpModernToLegacy();

        Assert.True(legacy.TryTake(out string returned));
        Assert.Equal("ORDER.PLACED|ORD-001", returned);
    }

    [Fact]
    public void Adds_the_headers_the_target_system_expects()
    {
        (LegacyBroker legacy, ModernBus modern, BridgeService bridge) = Build();
        legacy.Put("ORDER.PLACED|ORD-001");

        bridge.PumpLegacyToModern();
        modern.TryReceive(out Envelope crossed);

        // The legacy queue has no headers at all. Translation is not a copy:
        // the bridge must supply what the target requires and record where the
        // message came from, or the modern side cannot tell bridged traffic
        // from native traffic.
        Assert.Equal("legacy-queue", crossed.Headers["source"]);
        Assert.Equal("1.0", crossed.Headers["schema-version"]);
    }

    [Fact]
    public void Leaves_a_message_it_cannot_translate_on_the_source()
    {
        (LegacyBroker legacy, ModernBus modern, BridgeService bridge) = Build();

        // No separator, so no subject can be derived.
        legacy.Put("this is not in the expected format");

        Assert.Equal(0, bridge.PumpLegacyToModern());

        // Still there. Consuming an untranslatable message would destroy it,
        // and the operator would have nothing left to inspect or replay.
        Assert.Equal(1, legacy.Depth);
        Assert.Equal(0, modern.Depth);
    }
}
