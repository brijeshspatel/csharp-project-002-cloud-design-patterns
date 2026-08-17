namespace Choreography.Tests;

/// <summary>
/// What choreography guarantees: that a multi-service operation completes with
/// **nobody driving it** — each service reacts to the one event it cares about
/// and decides for itself what happens next.
///
/// And what it costs, asserted just as directly: **no component knows the
/// overall state**. Answering "what happened to this order?" means asking every
/// service and stitching the answers together, which is the price paid for
/// having no coordinator to ask.
/// </summary>
public class ChoreographyTests
{
    private sealed record Cast(
        EventJournal Journal,
        PaymentService Payment,
        InventoryService Inventory,
        ShippingService Shipping);

    private static Cast Assemble(bool stockAvailable = true)
    {
        EventJournal journal = new();
        PaymentService payment = new(journal);
        InventoryService inventory = new(journal, stockAvailable);
        ShippingService shipping = new(journal);
        return new Cast(journal, payment, inventory, shipping);
    }

    [Fact]
    public void Completes_the_operation_with_no_coordinator()
    {
        Cast cast = Assemble();

        cast.Journal.Publish(new OrderEvent(OrderEvent.Placed, "ORD-1042"));

        // Nothing called the services in turn. Each reacted, and the operation
        // ran to completion because the chain of reactions happens to close.
        Assert.Contains(cast.Journal.History, e => e.Kind == OrderEvent.Shipped);
    }

    [Fact]
    public void Each_service_reacts_only_to_the_event_it_cares_about()
    {
        Cast cast = Assemble();

        cast.Journal.Publish(new OrderEvent(OrderEvent.Placed, "ORD-1042"));

        Assert.Equal([OrderEvent.Placed], cast.Payment.Handled);
        Assert.Equal([OrderEvent.PaymentAccepted], cast.Inventory.Handled);
        Assert.Equal([OrderEvent.StockReserved], cast.Shipping.Handled);
    }

    [Fact]
    public void Records_the_events_in_the_order_they_were_published()
    {
        Cast cast = Assemble();

        cast.Journal.Publish(new OrderEvent(OrderEvent.Placed, "ORD-1042"));

        Assert.Equal(
            [OrderEvent.Placed, OrderEvent.PaymentAccepted, OrderEvent.StockReserved, OrderEvent.Shipped],
            cast.Journal.History.Select(e => e.Kind));
    }

    [Fact]
    public void Leaves_the_operation_incomplete_when_a_service_declines()
    {
        Cast cast = Assemble(stockAvailable: false);

        cast.Journal.Publish(new OrderEvent(OrderEvent.Placed, "ORD-1042"));

        // Inventory published nothing, so shipping never heard anything, so the
        // order simply stops. Nothing anywhere reports a failure — which is the
        // pattern's sharpest edge, not an omission in this model.
        Assert.DoesNotContain(cast.Journal.History, e => e.Kind == OrderEvent.StockReserved);
        Assert.Empty(cast.Shipping.Handled);
    }

    [Fact]
    public void No_component_knows_the_overall_state()
    {
        Cast cast = Assemble();

        cast.Journal.Publish(new OrderEvent(OrderEvent.Placed, "ORD-1042"));

        // Payment does not know it shipped. Shipping does not know it was paid.
        Assert.False(cast.Payment.Knows(OrderEvent.Shipped));
        Assert.False(cast.Inventory.Knows(OrderEvent.Shipped));
        Assert.False(cast.Shipping.Knows(OrderEvent.Placed));

        // And no service saw the whole thing, so "what happened to this order?"
        // has no single component that can answer it.
        Assert.All(
            new[] { cast.Payment.Handled, cast.Inventory.Handled, cast.Shipping.Handled },
            handled => Assert.Single(handled));
    }
}
