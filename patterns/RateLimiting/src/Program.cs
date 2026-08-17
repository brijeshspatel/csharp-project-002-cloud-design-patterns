using RateLimiting;

// A client of a geocoding API published at ten requests per second. It bursts,
// gets told to wait, waits exactly as long as it was told, and carries on -
// never provoking the throttling response the server would otherwise send.

Console.WriteLine("Rate Limiting - pacing yourself to stay inside somebody else's limit");
Console.WriteLine(new string('=', 70));
Console.WriteLine();

ManualClock clock = new(new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero));
TokenBucketRateLimiter limiter = new(capacity: 5, refillPerSecond: 10, clock);
GeocodingClient client = new(limiter);

Console.WriteLine("Phase 1: a burst, up to the bucket's capacity");
Console.WriteLine(new string('-', 70));
for (int i = 1; i <= 7; i++)
{
    Report($"request {i}", client.Geocode($"address {i}"));
}

Console.WriteLine();
Console.WriteLine("Phase 2: wait exactly as long as the limiter said, then continue");
Console.WriteLine(new string('-', 70));
TimeSpan wait = limiter.RetryAfter();
Console.WriteLine($"  limiter says wait {wait.TotalMilliseconds:0}ms");
clock.Advance(wait);
Report("request 8", client.Geocode("address 8"));

Console.WriteLine();
Console.WriteLine("Phase 3: a full second passes and the burst allowance returns");
Console.WriteLine(new string('-', 70));
clock.Advance(TimeSpan.FromSeconds(1));
for (int i = 9; i <= 12; i++)
{
    Report($"request {i}", client.Geocode($"address {i}"));
}

Console.WriteLine();
Console.WriteLine($"{client.Calls} calls actually reached the API; the rest were");
Console.WriteLine("never sent, which is the point - the limit was never provoked.");

static void Report(string label, GeocodeOutcome outcome)
{
    Console.WriteLine(outcome.Sent
        ? $"  {label}: sent, {outcome.Coordinates}"
        : $"  {label}: held back, retry after {outcome.RetryAfter.TotalMilliseconds:0}ms");
}
