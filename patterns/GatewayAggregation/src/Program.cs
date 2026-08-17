using GatewayAggregation;

// A mobile home screen needing four services. The gateway counts the round
// trips the client did not make, because in process four calls cost the same
// as one and the saving is otherwise invisible.

Console.WriteLine("Gateway Aggregation - four backends, one client round trip");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

Console.WriteLine("A healthy home screen");
Console.WriteLine(new string('-', 62));

AggregatingGateway gateway = new(new ProfileService(), new OrderService(), new OfferService(), new BasketService());
HomeScreen? screen = gateway.Fetch("CUST-1");

Console.WriteLine($"  profile:      {screen?.Profile}");
Console.WriteLine($"  orders:       {string.Join(", ", screen?.Orders ?? [])}");
Console.WriteLine($"  offers:       {string.Join(", ", screen?.Offers ?? [])}");
Console.WriteLine($"  basket items: {screen?.BasketItems}");
Console.WriteLine();
Console.WriteLine($"  client round trips: {gateway.ClientRoundTrips}");
Console.WriteLine($"  backend calls:      {gateway.BackendCalls}");
Console.WriteLine($"  round trips saved:  {gateway.RoundTripsSaved}");
Console.WriteLine();
Console.WriteLine("  The same four calls happen. They happen on the fast side of");
Console.WriteLine("  the slow link, which is the whole of the benefit.");

Console.WriteLine();
Console.WriteLine("The offers service is down");
Console.WriteLine(new string('-', 62));

AggregatingGateway degraded = new(
    new ProfileService(), new OrderService(), new OfferService(healthy: false), new BasketService());
HomeScreen? partial = degraded.Fetch("CUST-1");

Console.WriteLine($"  profile:      {partial?.Profile}");
Console.WriteLine($"  orders:       {string.Join(", ", partial?.Orders ?? [])}");
Console.WriteLine($"  offers:       {(partial?.Offers.Count == 0 ? "(none returned)" : string.Join(", ", partial?.Offers ?? []))}");
Console.WriteLine($"  unavailable:  {string.Join(", ", partial?.Unavailable ?? [])}");
Console.WriteLine();
Console.WriteLine("  The screen rendered. Aborting it because one optional panel");
Console.WriteLine("  failed would have cost the customer everything else.");
Console.WriteLine();
Console.WriteLine("  And the failure is named. Returning an empty offers list with");
Console.WriteLine("  no explanation would tell the client this customer has no");
Console.WriteLine("  offers - a different statement, and a false one.");

Console.WriteLine();
Console.WriteLine("A customer who does not exist");
Console.WriteLine(new string('-', 62));

AggregatingGateway unknown = new(new ProfileService(), new OrderService(), new OfferService(), new BasketService());
HomeScreen? nothing = unknown.Fetch("CUST-UNKNOWN");

Console.WriteLine($"  screen:        {(nothing is null ? "none" : "rendered")}");
Console.WriteLine($"  backend calls: {unknown.BackendCalls}");
Console.WriteLine("  No profile, no screen - and the other three were never asked,");
Console.WriteLine("  because there was nothing for them to answer about.");
