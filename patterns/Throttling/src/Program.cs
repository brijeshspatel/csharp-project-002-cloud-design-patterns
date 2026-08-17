using Throttling;

// A multi-tenant reporting API. One tenant is noisy and walks through all three
// decisions; a second tenant is unaffected, which is the point of per-tenant
// accounting. The clock is advanced by hand to cross a window boundary.

Console.WriteLine("Throttling - protecting a resource you own");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

ManualClock clock = new(new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero));
Throttle throttle = new(
    softLimit: 3,
    hardLimit: 5,
    window: TimeSpan.FromSeconds(60),
    clock);
ReportingService reports = new(throttle);

Console.WriteLine("A noisy tenant works through its allowance");
Console.WriteLine(new string('-', 62));
for (int request = 1; request <= 7; request++)
{
    Console.WriteLine($"  request {request}: {reports.RunReport("contoso")}");
}

Console.WriteLine();
Console.WriteLine("A second tenant is unaffected");
Console.WriteLine(new string('-', 62));
Console.WriteLine($"  fabrikam:  {reports.RunReport("fabrikam")}");

Console.WriteLine();
Console.WriteLine("The window rolls over and the allowance returns");
Console.WriteLine(new string('-', 62));
clock.Advance(TimeSpan.FromSeconds(60));
Console.WriteLine($"  contoso:   {reports.RunReport("contoso")}");

Console.WriteLine();
Console.WriteLine("Degrading before refusing is the pattern. A tenant served");
Console.WriteLine("stale figures has been served; one given an error has not.");
