namespace FederatedIdentity.Tests;

/// <summary>
/// What federated identity guarantees: that the application **never sees a
/// credential**. It receives a token it can check for itself, and the password
/// exists only between the user and the identity provider.
///
/// The asymmetry is asserted structurally, in the same shape as Gatekeeper's:
/// the provider holds secrets, the application holds none. An application with
/// its own password table has not federated anything.
/// </summary>
public class RelyingApplicationTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private sealed record Cast(IdentityProvider Provider, RelyingApplication Application, ManualClock Clock);

    private static Cast Assemble()
    {
        ManualClock clock = new(Noon);
        IdentityProvider provider = new("https://login.example.test", "signing-key-7", clock);
        provider.Enrol("ada", "correct horse battery staple");

        RelyingApplication application = new(provider.Issuer, provider.VerificationKey, clock);
        return new Cast(provider, application, clock);
    }

    [Fact]
    public void Accepts_a_token_the_identity_provider_issued()
    {
        Cast cast = Assemble();
        IdentityToken token = cast.Provider.Authenticate("ada", "correct horse battery staple", Lifetime)!.Value;

        Assert.Equal("ada", cast.Application.SignIn(token));
    }

    [Fact]
    public void Refuses_a_token_it_cannot_attribute_to_the_provider()
    {
        Cast cast = Assemble();

        // Right shape, right issuer, wrong signature — which is what a forged
        // token looks like from the application's side.
        IdentityToken forged = new("ada", cast.Provider.Issuer, "sig(forged)", Noon.AddHours(1));

        Assert.Null(cast.Application.SignIn(forged));
    }

    [Fact]
    public void Refuses_a_token_that_has_expired()
    {
        Cast cast = Assemble();
        IdentityToken token = cast.Provider.Authenticate("ada", "correct horse battery staple", Lifetime)!.Value;

        // Valid when issued, and used an hour later. A token with no expiry
        // check is a credential that never stops working.
        Assert.Equal("ada", cast.Application.SignIn(token));

        cast.Clock.Advance(TimeSpan.FromHours(1));

        Assert.Null(cast.Application.SignIn(token));
    }

    [Fact]
    public void Holds_no_credential_of_its_own()
    {
        Cast cast = Assemble();

        // The provider holds the password and the signing key. The application
        // holds a verification key, which is not a secret — an attacker who
        // takes it can check tokens and cannot mint them.
        Assert.Empty(cast.Application.SecretsHeld);
        Assert.NotEmpty(cast.Provider.SecretsHeld);
    }

    [Fact]
    public void Reads_the_user_from_the_token_without_asking_the_provider()
    {
        Cast cast = Assemble();
        IdentityToken token = cast.Provider.Authenticate("ada", "correct horse battery staple", Lifetime)!.Value;
        int before = cast.Provider.Verifications;

        cast.Application.SignIn(token);

        // No call back to the provider. That is what makes the provider's
        // availability a sign-in concern rather than a per-request one.
        Assert.Equal(before, cast.Provider.Verifications);
    }

    [Fact]
    public void Reports_why_a_token_was_refused()
    {
        Cast cast = Assemble();
        IdentityToken token = cast.Provider.Authenticate("ada", "correct horse battery staple", Lifetime)!.Value;

        cast.Application.SignIn(new IdentityToken("ada", "https://elsewhere.test", token.Signature, token.ExpiresAt));
        Assert.Contains("issuer", cast.Application.LastRefusal, StringComparison.Ordinal);

        cast.Clock.Advance(TimeSpan.FromHours(1));
        cast.Application.SignIn(token);
        Assert.Contains("expired", cast.Application.LastRefusal, StringComparison.Ordinal);
    }
}
