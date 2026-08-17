namespace ValetKey;

/// <summary>What a key permits. A key permits exactly one of these.</summary>
public enum AccessRight
{
    /// <summary>Read the named resource, and nothing else.</summary>
    Read,

    /// <summary>Write the named resource, and nothing else.</summary>
    Write,
}

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
/// A key to one resource, with one permission, until one instant.
///
/// **All three limits are load-bearing.** A key missing any one of them is the
/// thing this pattern replaces: unscoped is a key to the whole container,
/// unrestricted is a write key handed out for a download, and unexpiring is a
/// permanent credential living in a client that cannot keep it.
/// </summary>
/// <param name="Value">What the client presents.</param>
/// <param name="Resource">The one resource it names.</param>
/// <param name="AccessRight">The one thing it permits.</param>
/// <param name="ExpiresAt">When it stops working, whatever else is true.</param>
public readonly record struct ValetToken(
    string Value,
    string Resource,
    AccessRight AccessRight,
    DateTimeOffset ExpiresAt);

/// <summary>
/// The application: the only component that knows who the caller is and what
/// they may do, and therefore the only one that may issue a key.
///
/// It authorises **once**, at issue time, and then steps out of the data path
/// entirely. That is the whole point: a large upload or download no longer
/// streams through the application, consuming a connection and a slice of
/// capacity for the duration.
/// </summary>
public sealed class ValetKeyIssuer
{
    private readonly IClock clock;
    private readonly HashSet<string> issued = [];
    private int counter;

    /// <summary>Creates an issuer reading <paramref name="clock"/>.</summary>
    public ValetKeyIssuer(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        this.clock = clock;
    }

    /// <summary>How many keys have been issued.</summary>
    public int Issued => issued.Count;

    /// <summary>
    /// Issues a key for one resource, one permission and one lifetime. Whatever
    /// authorisation the application performs happens here, before this returns.
    /// </summary>
    public ValetToken Issue(string resource, AccessRight permission, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(resource);

        counter++;
        string value = $"key-{counter:0000}";
        issued.Add(value);

        return new ValetToken(value, resource, permission, clock.UtcNow + lifetime);
    }

    /// <summary>
    /// Revokes a key before its expiry. Possible here because the issuer keeps a
    /// registry; a key that is a self-contained signature — which is what most
    /// real implementations use — cannot be revoked this way at all.
    /// </summary>
    public void Revoke(ValetToken token) => issued.Remove(token.Value);

    /// <summary>
    /// Checks a presented key against what is being attempted. Called by the
    /// store, not by the application: this is the storage service's check.
    /// </summary>
    public bool IsValid(ValetToken token, string resource, AccessRight wanted) =>
        issued.Contains(token.Value)
        && string.Equals(token.Resource, resource, StringComparison.Ordinal)
        && token.AccessRight == wanted
        && clock.UtcNow < token.ExpiresAt;
}

/// <summary>
/// The store the client talks to directly, holding the data and **no idea who
/// anyone is**. It knows only whether the key presented permits what is being
/// attempted.
///
/// That ignorance is the design. The store cannot consult a user directory, and
/// it does not need to: the application already decided, and the key carries
/// the decision.
/// </summary>
public sealed class ResourceStore
{
    private readonly ValetKeyIssuer issuer;
    private readonly Dictionary<string, string> resources = [];

    /// <summary>Creates a store validating keys against <paramref name="issuer"/>.</summary>
    public ResourceStore(ValetKeyIssuer issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        this.issuer = issuer;
    }

    /// <summary>What the store holds for <paramref name="resource"/>, unguarded — for tests only.</summary>
    public string? ContentOf(string resource) =>
        resources.TryGetValue(resource, out string? content) ? content : null;

    /// <summary>Reads <paramref name="resource"/>, if the key permits exactly that.</summary>
    /// <exception cref="UnauthorizedAccessException">The key does not permit it.</exception>
    public string? Read(ValetToken token, string resource)
    {
        Authorise(token, resource, AccessRight.Read);
        return ContentOf(resource);
    }

    /// <summary>Writes <paramref name="resource"/>, if the key permits exactly that.</summary>
    /// <exception cref="UnauthorizedAccessException">The key does not permit it.</exception>
    public void Write(ValetToken token, string resource, string content)
    {
        Authorise(token, resource, AccessRight.Write);
        resources[resource] = content;
    }

    private void Authorise(ValetToken token, string resource, AccessRight wanted)
    {
        if (!issuer.IsValid(token, resource, wanted))
        {
            throw new UnauthorizedAccessException(
                $"The key presented does not permit {wanted} on '{resource}'.");
        }
    }
}
