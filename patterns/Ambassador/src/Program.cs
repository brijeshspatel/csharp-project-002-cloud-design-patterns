using Ambassador;

// An application asking for a price through a helper that handles the network.
// The application makes one call; the ambassador makes as many attempts as the
// network requires, and the application never learns which.

Console.WriteLine("Ambassador - the application asks once");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

TimeSpan backoff = TimeSpan.FromMilliseconds(200);
DateTimeOffset noon = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

Console.WriteLine("A healthy backend");
Console.WriteLine(new string('-', 60));

PricingBackend healthy = new("pricing", _ => true);
PricingAmbassador first = new(healthy, new ManualClock(noon), attemptLimit: 3, backoff);
PriceQuote? quick = first.Ask("SKU-1042");

Report(first, healthy, quick);

Console.WriteLine();
Console.WriteLine("A backend that answers on the third attempt");
Console.WriteLine(new string('-', 60));

PricingBackend flaky = new("pricing", attempt => attempt > 2);
PricingAmbassador second = new(flaky, new ManualClock(noon), attemptLimit: 3, backoff);
PriceQuote? eventual = second.Ask("SKU-1042");

Report(second, flaky, eventual);

Console.WriteLine();
Console.WriteLine("The application's view of those two calls");
Console.WriteLine(new string('-', 60));
Console.WriteLine($"  first call returned:  {Describe(quick)}");
Console.WriteLine($"  second call returned: {Describe(eventual)}");
Console.WriteLine($"  identical:            {quick == eventual}");
Console.WriteLine();
Console.WriteLine("  The application cannot tell that one of those took three");
Console.WriteLine("  attempts. That is what lets retry policy be tuned, or made");
Console.WriteLine("  consistent across services in four languages, without any");
Console.WriteLine("  application being redeployed.");

Console.WriteLine();
Console.WriteLine("A backend that never answers");
Console.WriteLine(new string('-', 60));

PricingBackend dead = new("pricing", _ => false);
PricingAmbassador third = new(dead, new ManualClock(noon), attemptLimit: 3, backoff);
PriceQuote? nothing = third.Ask("SKU-1042");

Report(third, dead, nothing);
Console.WriteLine($"  the ambassador says: {third.LastFailure}");
Console.WriteLine();
Console.WriteLine("  Three attempts, then it stops. Retrying for ever would hold the");
Console.WriteLine("  application's thread indefinitely - worse than the failure it");
Console.WriteLine("  was hiding.");

Console.WriteLine();
Console.WriteLine("An ambassador is conventionally deployed as a sidecar: same host,");
Console.WriteLine("same lifecycle, separate process. Sidecar is the shape; this is one");
Console.WriteLine("job that shape does.");

static void Report(PricingAmbassador ambassador, PricingBackend backend, PriceQuote? quote)
{
    Console.WriteLine($"  application calls:   {ambassador.CallsFromApplication}");
    Console.WriteLine($"  network attempts:    {ambassador.AttemptsMade}");
    Console.WriteLine($"  attempts absorbed:   {ambassador.AttemptsAbsorbed}");
    Console.WriteLine($"  backoff accumulated: {ambassador.TotalBackoff.TotalMilliseconds:0}ms");
    Console.WriteLine($"  result:              {Describe(quote)}");
}

static string Describe(PriceQuote? quote) =>
    quote is { } value ? $"{value.Sku} at {value.Price:0.00}" : "nothing";
