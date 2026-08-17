namespace AntiCorruptionLayer.Tests;

/// <summary>
/// What an anti-corruption layer guarantees: that the legacy system's model —
/// its names, its units, its status codes — **stops at the boundary**, and that
/// the modern side never learns any of it.
///
/// The test that matters most is the negative one. Translating correctly is
/// necessary; what makes this a pattern rather than a mapping function is that
/// no legacy representation survives the crossing.
/// </summary>
public class OrderTranslatorTests
{
    private static LegacyOrderRecord Record(string statusCode = "A") =>
        new("0000001042", "C0007", 54900, statusCode, "20260818");

    private static OrderTranslator Translator() => new(new LegacyMainframe());

    [Fact]
    public void Translates_a_legacy_record_into_the_modern_model()
    {
        ModernOrder order = Translator().ToModern(Record());

        Assert.Equal("ORD-1042", order.Reference);
        Assert.Equal("CUST-7", order.CustomerId);
        Assert.Equal("placed", order.Status);
        Assert.Equal(new DateOnly(2026, 8, 18), order.PlacedOn);
    }

    [Fact]
    public void Translates_a_modern_order_back_into_the_legacy_shape()
    {
        OrderTranslator translator = Translator();
        ModernOrder order = new("ORD-1042", "CUST-7", 549.00m, "shipped", new DateOnly(2026, 8, 18));

        LegacyOrderRecord record = translator.ToLegacy(order);

        // Both directions, because a boundary that only translates inward
        // leaves every write path reaching around it.
        Assert.Equal("0000001042", record.OrderNumber);
        Assert.Equal("C0007", record.CustomerNumber);
        Assert.Equal(54900, record.AmountInPence);
        Assert.Equal("S", record.StatusCode);
        Assert.Equal("20260818", record.OrderDate);
    }

    [Fact]
    public void Converts_the_units_the_legacy_system_uses()
    {
        ModernOrder order = Translator().ToModern(Record());

        // Pence to pounds. A units mismatch that crosses a boundary is the
        // quietest possible defect: everything runs, and the numbers are wrong
        // by a factor of a hundred.
        Assert.Equal(549.00m, order.Amount);
    }

    [Fact]
    public void Maps_a_legacy_status_the_modern_model_has_no_concept_of()
    {
        OrderTranslator translator = Translator();

        // "B" means held in the overnight batch — a mainframe scheduling detail
        // the modern model has no concept of and should never acquire.
        ModernOrder order = translator.ToModern(Record(statusCode: "B"));

        Assert.Equal("placed", order.Status);
        Assert.Contains(
            translator.Concessions,
            note => note.Contains("overnight batch", StringComparison.Ordinal));
    }

    [Fact]
    public void Keeps_legacy_types_out_of_the_modern_model()
    {
        ModernOrder order = Translator().ToModern(Record(statusCode: "X"));

        // No legacy representation survives the crossing: not the padded
        // reference, not the pence, not the status code. This is what separates
        // a boundary from a mapping function that happens to rename things.
        Assert.DoesNotContain("0000001042", order.Reference, StringComparison.Ordinal);
        Assert.NotEqual(54900m, order.Amount);
        Assert.DoesNotContain(order.Status, (string[])["A", "B", "S", "X"]);
        Assert.Equal("cancelled", order.Status);
    }
}
