using ValetKey;

// The application authorises once and issues a key. The client then talks to
// the store directly, and the store knows nothing except what the key permits.

Console.WriteLine("Valet Key - one resource, one permission, one expiry");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

ManualClock clock = new(new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero));
ValetKeyIssuer issuer = new(clock);
ResourceStore store = new(issuer);

Console.WriteLine("The application issues an upload key");
Console.WriteLine(new string('-', 60));

ValetToken upload = issuer.Issue("uploads/invoice-1042.pdf", AccessRight.Write, TimeSpan.FromMinutes(15));

Console.WriteLine($"  resource:   {upload.Resource}");
Console.WriteLine($"  permission: {upload.AccessRight}");
Console.WriteLine($"  expires:    {upload.ExpiresAt:HH:mm} (now {clock.UtcNow:HH:mm})");

Console.WriteLine();
Console.WriteLine("The client uploads directly to the store");
Console.WriteLine(new string('-', 60));

store.Write(upload, "uploads/invoice-1042.pdf", "PDF-BYTES");
Console.WriteLine($"  written, and the application never saw the bytes");

Console.WriteLine();
Console.WriteLine("Each limit refuses on its own");
Console.WriteLine(new string('-', 60));

Refused("another resource", () => store.Write(upload, "uploads/someone-elses.pdf", "PDF-BYTES"));
Refused("a read with a write-only key", () => store.Read(upload, "uploads/invoice-1042.pdf"));

ValetToken forged = new("forged-value", "uploads/invoice-1042.pdf", AccessRight.Write, clock.UtcNow.AddYears(1));
Refused("a key never issued", () => store.Write(forged, "uploads/invoice-1042.pdf", "PDF-BYTES"));

clock.Advance(TimeSpan.FromMinutes(16));
Refused("the same key, sixteen minutes later", () => store.Write(upload, "uploads/invoice-1042.pdf", "PDF-BYTES"));

Console.WriteLine();
Console.WriteLine("The expiry is the only limit nobody has to remember to act on:");
Console.WriteLine("a key that has run out is refused whatever else is true.");

static void Refused(string what, Action attempt)
{
    try
    {
        attempt();
        Console.WriteLine($"  {what}: ALLOWED  <- the key was not scoped as it should be");
    }
    catch (UnauthorizedAccessException refusal)
    {
        Console.WriteLine($"  {what}: refused");
        Console.WriteLine($"    {refusal.Message}");
    }
}
