namespace FederatedIdentity;

/// <summary>Time, so expiry can be tested without waiting for it.</summary>
public interface IClock
{
    /// <summary>The current instant.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>A clock that moves only when told to.</summary>
public sealed class ManualClock : IClock
{
    /// <summary>Creates a clock reading <paramref name="start"/>.</summary>
    public ManualClock(DateTimeOffset start) => UtcNow = start;

    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; private set; }

    /// <summary>Moves the clock forward.</summary>
    public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
}

/// <summary>
/// What the application receives instead of a password.
///
/// It says who the user is, who vouches for that, when it stops being true, and
/// carries a signature the application can check **without asking anybody**.
/// </summary>
/// <param name="Subject">Who the user is.</param>
/// <param name="Issuer">Who vouches for it.</param>
/// <param name="Signature">Proof the issuer produced it.</param>
/// <param name="ExpiresAt">When it stops being true.</param>
public readonly record struct IdentityToken(
    string Subject,
    string Issuer,
    string Signature,
    DateTimeOffset ExpiresAt);

/// <summary>
/// The identity provider: the only component that ever sees a password.
///
/// It authenticates users and issues signed tokens. Every application that
/// trusts it gets sign-in, password policy, multi-factor, lockout and account
/// recovery without implementing any of them — which is the practical reason
/// this pattern is nearly universal.
/// </summary>
public sealed class IdentityProvider
{
    private readonly Dictionary<string, string> credentials = [];
    private readonly List<string> secretsHeld = [];
    private readonly string signingKey;
    private readonly IClock clock;

    /// <summary>Creates a provider issuing tokens as <paramref name="issuer"/>.</summary>
    public IdentityProvider(string issuer, string signingKey, IClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);
        ArgumentNullException.ThrowIfNull(clock);

        Issuer = issuer;
        this.signingKey = signingKey;
        this.clock = clock;
        secretsHeld.Add("token signing key");
    }

    /// <summary>Who this provider says it is.</summary>
    public string Issuer { get; }

    /// <summary>
    /// What an application needs to check a signature.
    ///
    /// **In this model it equals the signing key**, because the signature is a
    /// string comparison rather than real cryptography. A real provider publishes
    /// an asymmetric public key, so holding it lets an application *verify*
    /// tokens and never *mint* them — which is the property the tests here
    /// assert and this model approximates.
    /// </summary>
    public string VerificationKey => signingKey;

    /// <summary>What this component holds that an attacker would want.</summary>
    public IReadOnlyList<string> SecretsHeld => secretsHeld;

    /// <summary>How many times an application has called back to it.</summary>
    public int Verifications { get; private set; }

    /// <summary>Registers a user. **Only this component ever sees the password.**</summary>
    public void Enrol(string user, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        credentials[user] = password;

        if (!secretsHeld.Contains("user credentials"))
        {
            secretsHeld.Add("user credentials");
        }
    }

    /// <summary>Authenticates a user and issues a token, or nothing.</summary>
    public IdentityToken? Authenticate(string user, string password, TimeSpan lifetime)
    {
        if (!credentials.TryGetValue(user, out string? known) ||
            !string.Equals(known, password, StringComparison.Ordinal))
        {
            return null;
        }

        DateTimeOffset expiresAt = clock.UtcNow + lifetime;
        return new IdentityToken(user, Issuer, Sign(user, Issuer, expiresAt, signingKey), expiresAt);
    }

    /// <summary>A call back to the provider — which token validation does not need.</summary>
    public bool Introspect(IdentityToken token)
    {
        Verifications++;
        return string.Equals(
            token.Signature,
            Sign(token.Subject, token.Issuer, token.ExpiresAt, signingKey),
            StringComparison.Ordinal);
    }

    internal static string Sign(string subject, string issuer, DateTimeOffset expiresAt, string key) =>
        $"sig({subject}|{issuer}|{expiresAt:O}|{key})";
}

/// <summary>
/// The application. It **holds no credential of its own** — no password table,
/// no hashing, no reset flow — and that absence is the pattern.
///
/// It checks a token entirely locally: the issuer it trusts, the signature it
/// can verify, and the expiry against its own clock. **It does not call the
/// provider to validate**, which is why the provider's availability is a
/// sign-in concern rather than a per-request one.
///
/// **What this is not.** Gatekeeper also splits a system so the exposed half
/// holds nothing worth stealing, and its subject is network reachability; this
/// one's is authentication. Valet Key hands out a scoped, expiring key to a
/// resource; a token here asserts *who somebody is* and leaves authorisation to
/// the application.
/// </summary>
public sealed class RelyingApplication
{
    private readonly string trustedIssuer;
    private readonly string verificationKey;
    private readonly IClock clock;

    // Empty, and it stays empty. Held as a field so that "what does this
    // component hold?" is a question about its state.
    private readonly List<string> secretsHeld = [];

    /// <summary>Creates an application trusting <paramref name="trustedIssuer"/>.</summary>
    public RelyingApplication(string trustedIssuer, string verificationKey, IClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedIssuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(verificationKey);
        ArgumentNullException.ThrowIfNull(clock);

        this.trustedIssuer = trustedIssuer;
        this.verificationKey = verificationKey;
        this.clock = clock;
    }

    /// <summary>
    /// What this component holds that an attacker would want: **nothing**. The
    /// verification key is not a secret — it checks tokens and cannot mint them.
    /// </summary>
    public IReadOnlyList<string> SecretsHeld => secretsHeld;

    /// <summary>Why the last token was refused, where it was.</summary>
    public string LastRefusal { get; private set; } = string.Empty;

    /// <summary>
    /// Signs a user in from a token, returning who they are — or nothing, with
    /// a reason.
    /// </summary>
    public string? SignIn(IdentityToken token)
    {
        if (!string.Equals(token.Issuer, trustedIssuer, StringComparison.Ordinal))
        {
            LastRefusal = $"the issuer '{token.Issuer}' is not trusted here";
            return null;
        }

        string expected = IdentityProvider.Sign(token.Subject, token.Issuer, token.ExpiresAt, verificationKey);
        if (!string.Equals(token.Signature, expected, StringComparison.Ordinal))
        {
            LastRefusal = "the signature does not match the issuer's key";
            return null;
        }

        if (clock.UtcNow >= token.ExpiresAt)
        {
            LastRefusal = $"the token expired at {token.ExpiresAt:HH:mm}";
            return null;
        }

        LastRefusal = string.Empty;
        return token.Subject;
    }
}
