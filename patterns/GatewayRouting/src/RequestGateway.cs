namespace GatewayRouting;

/// <summary>One request as the client sent it, to the single public endpoint.</summary>
/// <param name="Path">What was asked for. The only thing routing looks at.</param>
/// <param name="Body">What came with it.</param>
public readonly record struct ClientRequest(string Path, string Body);

/// <summary>What came back, and which service produced it.</summary>
/// <param name="ServedBy">The service that handled it, or empty where none did.</param>
/// <param name="Body">The answer.</param>
/// <param name="Found">Whether any route matched.</param>
public readonly record struct ServiceResponse(string ServedBy, string Body, bool Found);

/// <summary>
/// One service behind the gateway. It has no idea a gateway exists, which is
/// the property that lets services be added, split, versioned or replaced
/// without any client learning about it.
/// </summary>
public sealed class BackendService
{
    /// <summary>Creates a service that answers under <paramref name="name"/>.</summary>
    public BackendService(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>What this service is called.</summary>
    public string Name { get; }

    /// <summary>How many requests reached it — the count a refusal must not increase.</summary>
    public int Handled { get; private set; }

    /// <summary>Answers a request.</summary>
    public ServiceResponse Handle(ClientRequest request)
    {
        Handled++;
        return new ServiceResponse(Name, $"{Name} handled {request.Path}", Found: true);
    }
}

/// <summary>
/// Path prefix to service, and **nothing else**. It is deliberately the only
/// place that knows the topology: everything a gateway is for follows from that
/// knowledge living in exactly one place.
///
/// **Longest prefix wins**, so `/v2/orders` and `/orders` can coexist without
/// registration order deciding the outcome. Order-dependent routing is a defect
/// that hides until somebody rearranges a configuration file.
/// </summary>
public sealed class RouteTable
{
    private readonly Dictionary<string, BackendService> routes = [];
    private readonly List<string> order = [];

    /// <summary>The prefixes registered, in registration order.</summary>
    public IReadOnlyList<string> Prefixes => order;

    /// <summary>Registers a prefix. Adding one requires no client to change.</summary>
    public void Add(string prefix, BackendService service)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(service);

        if (!routes.ContainsKey(prefix))
        {
            order.Add(prefix);
        }

        routes[prefix] = service;
    }

    /// <summary>Finds the service owning <paramref name="path"/>, longest prefix first.</summary>
    public BackendService? Resolve(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        BackendService? best = null;
        int longest = -1;

        foreach (KeyValuePair<string, BackendService> route in routes)
        {
            if (path.StartsWith(route.Key, StringComparison.Ordinal) && route.Key.Length > longest)
            {
                longest = route.Key.Length;
                best = route.Value;
            }
        }

        return best;
    }
}

/// <summary>
/// The single endpoint clients talk to. It answers exactly one question —
/// **which service handles this request** — and then gets out of the way.
///
/// **An unmatched path is refused, not guessed at.** A gateway that falls
/// through to something plausible turns a client typo into a real request
/// against the wrong service, and the resulting failure is attributed to that
/// service rather than to the routing.
///
/// **What this is not.** Gateway Aggregation calls several services and combines
/// their answers; this forwards one request to one service. Gateway Offloading
/// moves cross-cutting work off the services; this does none of their work at
/// all. Gatekeeper exists to make the back end unreachable; the services here
/// are perfectly reachable and simply are not addressed directly. Backends for
/// Frontends has no single endpoint, which is the whole of its point.
/// </summary>
public sealed class RequestGateway
{
    private readonly RouteTable routes;

    /// <summary>Creates a gateway over <paramref name="routes"/>.</summary>
    public RequestGateway(RouteTable routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        this.routes = routes;
    }

    /// <summary>The prefixes this gateway can route, for inspection.</summary>
    public IReadOnlyList<string> Routes => routes.Prefixes;

    /// <summary>Routes one request, or refuses it.</summary>
    public ServiceResponse Send(ClientRequest request)
    {
        BackendService? service = routes.Resolve(request.Path);

        if (service is null)
        {
            return new ServiceResponse(
                string.Empty,
                $"no route matches '{request.Path}'",
                Found: false);
        }

        return service.Handle(request);
    }
}
