using PublisherSubscriber;

// An order is placed. Fulfilment, billing and analytics all need to know, and
// the ordering service does not know any of them exist. Billing is broken, and
// analytics - which subscribed after it - still receives the event.

Console.WriteLine("Publisher-Subscriber - every subscriber gets every event");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

Topic orders = new();

orders.Subscribe("fulfilment", order =>
    Console.WriteLine($"    fulfilment: picking {order.Reference}"));

orders.Subscribe("billing", order =>
    throw new InvalidOperationException($"billing is down, could not invoice {order.Reference}"));

orders.Subscribe("analytics", order =>
    Console.WriteLine($"    analytics:  recorded {order.Amount:C} for {order.Reference}"));

Console.WriteLine($"{orders.SubscriberCount} subscribers listening");
Console.WriteLine();

Publish(new OrderPlaced("ORD-001", 42.50m));
Publish(new OrderPlaced("ORD-002", 19.99m));

Console.WriteLine("An audit subscriber joins for one order, then its subscription is disposed");
Console.WriteLine(new string('-', 60));

// Subscribe returns a disposable handle; disposing it removes only this registration.
using (IDisposable temporary = orders.Subscribe("audit", order =>
    Console.WriteLine($"    audit:      logged {order.Reference}")))
{
    Publish(new OrderPlaced("ORD-003", 7.25m));
}

Console.WriteLine($"After disposing the audit subscription: {orders.SubscriberCount} listening");
Publish(new OrderPlaced("ORD-004", 3.10m));

Console.WriteLine();
Console.WriteLine("The ordering service never referenced fulfilment, billing, analytics");
Console.WriteLine("or audit. Adding or removing a listener changed nothing here.");

void Publish(OrderPlaced order)
{
    Console.WriteLine($"  publishing {order.Reference}");
    PublishResult result = orders.Publish(order);
    Console.WriteLine($"    delivered to {result.Delivered}, {result.Failures.Count} failed");
    foreach (SubscriberFailure failure in result.Failures)
    {
        Console.WriteLine($"    failure:    {failure.Subscriber} - {failure.Reason}");
    }

    Console.WriteLine();
}
