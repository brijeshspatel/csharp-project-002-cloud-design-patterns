using FederatedIdentity;

// A user signs in with an identity provider. The application receives a token,
// checks it locally, and never sees a password.

Console.WriteLine("Federated Identity - the application never sees a credential");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

ManualClock clock = new(new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero));
IdentityProvider provider = new("https://login.example.test", "signing-key-7", clock);
provider.Enrol("ada", "correct horse battery staple");

RelyingApplication application = new(provider.Issuer, provider.VerificationKey, clock);

Console.WriteLine("What each component holds");
Console.WriteLine(new string('-', 62));
Console.WriteLine($"  identity provider: {string.Join(", ", provider.SecretsHeld)}");
Console.WriteLine($"  application:       {(application.SecretsHeld.Count == 0 ? "nothing" : string.Join(", ", application.SecretsHeld))}");
Console.WriteLine();
Console.WriteLine("  The application has a verification key, which is not a secret:");
Console.WriteLine("  it checks tokens and cannot mint them.");

Console.WriteLine();
Console.WriteLine("The user authenticates with the provider");
Console.WriteLine(new string('-', 62));

IdentityToken token = provider.Authenticate("ada", "correct horse battery staple", TimeSpan.FromMinutes(30))!.Value;
Console.WriteLine($"  subject:  {token.Subject}");
Console.WriteLine($"  issuer:   {token.Issuer}");
Console.WriteLine($"  expires:  {token.ExpiresAt:HH:mm}");
Console.WriteLine("  The password went to the provider. The application was not involved.");

Console.WriteLine();
Console.WriteLine("The application signs the user in");
Console.WriteLine(new string('-', 62));

int before = provider.Verifications;
Console.WriteLine($"  signed in as: {application.SignIn(token)}");
Console.WriteLine($"  calls back to the provider: {provider.Verifications - before}");
Console.WriteLine("  Checked locally: trusted issuer, matching signature, unexpired.");
Console.WriteLine("  The provider's availability is a sign-in concern, not a");
Console.WriteLine("  per-request one.");

Console.WriteLine();
Console.WriteLine("Three tokens that are refused");
Console.WriteLine(new string('-', 62));

Refuse("a forged signature", new IdentityToken("ada", provider.Issuer, "sig(forged)", token.ExpiresAt));
Refuse("an issuer we do not trust", new IdentityToken("ada", "https://elsewhere.test", token.Signature, token.ExpiresAt));

clock.Advance(TimeSpan.FromHours(1));
Refuse("the same good token, an hour later", token);

Console.WriteLine();
Console.WriteLine("A token with no expiry check would be a credential that never");
Console.WriteLine("stops working - which is the thing a password at least gets");
Console.WriteLine("rotated for.");

void Refuse(string what, IdentityToken candidate)
{
    string? result = application.SignIn(candidate);
    Console.WriteLine($"  {what}:");
    Console.WriteLine($"    {(result is null ? $"refused - {application.LastRefusal}" : $"ACCEPTED as {result}")}");
}
