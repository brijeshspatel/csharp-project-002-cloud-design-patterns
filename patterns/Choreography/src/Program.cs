using Choreography;

// The same order fulfilment the Saga pattern coordinates - payment, inventory,
// shipping - with nobody coordinating it.

Console.WriteLine("Choreography - the same operation, with nobody in charge");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

EventJournal journal = new();
PaymentService payment = new(journal);
InventoryService inventory = new(journal, stockAvailable: true);
ShippingService shipping = new(journal);

Console.WriteLine("One event is published, and nothing else is called");
Console.WriteLine(new string('-', 62));

journal.Publish(new OrderEvent(OrderEvent.Placed, "ORD-1042"));

foreach (OrderEvent announced in journal.History)
{
    Console.WriteLine($"  {announced.Kind,-16} {announced.OrderId}");
}

Console.WriteLine();
Console.WriteLine("Each service reacted to one event and decided for itself");
Console.WriteLine(new string('-', 62));
Console.WriteLine($"  payment   handled: {string.Join(", ", payment.Handled)}");
Console.WriteLine($"  inventory handled: {string.Join(", ", inventory.Handled)}");
Console.WriteLine($"  shipping  handled: {string.Join(", ", shipping.Handled)}");

Console.WriteLine();
Console.WriteLine("Now ask any of them what happened to the order");
Console.WriteLine(new string('-', 62));
Console.WriteLine($"  payment:   did it ship?    {payment.Knows(OrderEvent.Shipped)}");
Console.WriteLine($"  inventory: did it ship?    {inventory.Knows(OrderEvent.Shipped)}");
Console.WriteLine($"  shipping:  was it paid?    {shipping.Knows(OrderEvent.PaymentAccepted)}");
Console.WriteLine();
Console.WriteLine("  Nobody can answer. The state of the order exists only as the");
Console.WriteLine("  union of three services' partial views, and there is no");
Console.WriteLine("  component whose job is to hold it.");

Console.WriteLine();
Console.WriteLine("The same order, with no stock");
Console.WriteLine(new string('-', 62));

EventJournal second = new();
PaymentService payment2 = new(second);
InventoryService inventory2 = new(second, stockAvailable: false);
ShippingService shipping2 = new(second);

second.Publish(new OrderEvent(OrderEvent.Placed, "ORD-1043"));

foreach (OrderEvent announced in second.History)
{
    Console.WriteLine($"  {announced.Kind,-16} {announced.OrderId}");
}

Console.WriteLine();
Console.WriteLine($"  shipping handled: {(shipping2.Handled.Count == 0 ? "nothing" : string.Join(", ", shipping2.Handled))}");
Console.WriteLine("  The customer has been charged and the order stops here.");
Console.WriteLine("  No error was raised, because nobody was waiting for anything.");
Console.WriteLine();
Console.WriteLine("That silence is choreography's sharpest edge. An orchestrator");
Console.WriteLine("would have noticed the step it was waiting on never came back.");
