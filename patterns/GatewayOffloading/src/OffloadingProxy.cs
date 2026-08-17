namespace GatewayOffloading;

/// <summary>One request arriving at the edge.</summary>
/// <param name="Path">Which service it is for.</param>
/// <param name="Token">The caller's credential, which only the edge inspects.</param>
/// <param name="Body">What was asked.</param>
public readonly record struct EdgeRequest(string Path, string Token, string Body);

/// <summary>What the edge returns to the client.</summary>
/// <param name="Allowed">Whether it got past the edge at all.</param>
/// <param name="Body">The answer, compressed where it got through.</param>
/// <param name="Compressed">Whether the edge compressed it.</param>
/// <param name="Reason">Why it was refused, where it was.</param>
public readonly record struct EdgeResult(bool Allowed, string Body, bool Compressed, string Reason);

/// <summary>
/// A service behind the proxy, doing **only its own work**.
///
/// It has no token validation and no compression — not as a simplification, but
/// as the point. A service that still validated tokens while the proxy also
/// validated them would prove that nothing had been offloaded; the proxy would
/// be a hop, and the code would still be written nine times.
///
/// The test that matters calls this directly with no token and expects an
/// answer.
/// </summary>
public sealed class ApplicationService
{
    /// <summary>Creates a service named <paramref name="name"/>.</summary>
    public ApplicationService(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>What this service is called.</summary>
    public string Name { get; }

    /// <summary>How many requests reached it.</summary>
    public int Handled { get; private set; }

    /// <summary>Does the work, and nothing else. Notably it never reads the token.</summary>
    public string Handle(EdgeRequest request)
    {
        Handled++;
        return $"{Name} answered '{request.Body}'";
    }
}

/// <summary>
/// The edge. It performs the work **every** service would otherwise perform —
/// here, token validation and response compression — and then forwards to the
/// one service that owns the request.
///
/// The saving is not per request; it is per service, per concern, and it is
/// paid in code that does not exist. Two concerns across three services would
/// be six implementations to write, test, patch and keep consistent. Done once
/// at the edge it is two.
///
/// **What this is not.** Gatekeeper also refuses requests at the edge, and its
/// subject is entirely different: there, the back end is unreachable and holds
/// a credential the edge does not, so a compromised edge yields nothing. Here
/// the services are perfectly reachable and simply have less code in them.
/// Gateway Routing decides which service; this does work on the way. Gateway
/// Aggregation calls several services; this forwards to one.
/// </summary>
public sealed class OffloadingProxy
{
    private readonly Dictionary<string, ApplicationService> services = [];
    private readonly List<string> concerns = ["token validation", "response compression"];

    /// <summary>What the edge does on every service's behalf, named.</summary>
    public IReadOnlyList<string> Concerns => concerns;

    /// <summary>How many concerns are handled here rather than in every service.</summary>
    public int ConcernsAtEdge => concerns.Count;

    /// <summary>How many services sit behind the proxy.</summary>
    public int ServicesBehind => services.Count;

    /// <summary>
    /// Implementations that do not have to exist — the benefit, as a number.
    /// Without the proxy each service implements each concern; with it, the
    /// edge implements each concern once.
    /// </summary>
    public int ImplementationsAvoided => (ConcernsAtEdge * ServicesBehind) - ConcernsAtEdge;

    /// <summary>Puts a service behind the proxy at <paramref name="path"/>.</summary>
    public void Register(string path, ApplicationService service)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(service);
        services[path] = service;
    }

    /// <summary>
    /// Validates, forwards, compresses. A request that fails validation never
    /// reaches a service — which spares every service behind the proxy both the
    /// code and the traffic.
    /// </summary>
    public EdgeResult Send(EdgeRequest request)
    {
        if (!IsValid(request.Token))
        {
            return new EdgeResult(
                Allowed: false,
                Body: string.Empty,
                Compressed: false,
                Reason: "the token is missing or expired");
        }

        if (!services.TryGetValue(request.Path, out ApplicationService? service))
        {
            return new EdgeResult(false, string.Empty, false, $"no service at '{request.Path}'");
        }

        string plain = service.Handle(request);

        // Compression belongs here because the edge is what knows the transport
        // and what the client accepts. A service compressing its own responses
        // would be guessing on behalf of a client it never talks to.
        return new EdgeResult(true, Compress(plain), Compressed: true, Reason: string.Empty);
    }

    private static bool IsValid(string token) =>
        string.Equals(token, "token-valid", StringComparison.Ordinal);

    private static string Compress(string body) => $"gzip({body})";
}
