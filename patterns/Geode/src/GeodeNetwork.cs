namespace Geode;

/// <summary>Where a client is asking from.</summary>
/// <param name="Region">Its region, which decides which node is nearest.</param>
public readonly record struct ClientLocation(string Region);

/// <summary>An answer, and **which node produced it**.</summary>
/// <param name="ServedBy">The node that answered.</param>
/// <param name="Value">What it said.</param>
/// <param name="Found">Whether anything was found.</param>
public readonly record struct GeodeResponse(string ServedBy, string? Value, bool Found);

/// <summary>
/// One node of the network, in one region, holding **the same data as every
/// other node**.
///
/// That is the defining property and the source of both the benefit and the
/// cost: any node can serve any request, and a node that has not received the
/// latest write serves the previous value confidently.
/// </summary>
public sealed class GeodeNode
{
    private readonly Dictionary<string, string> data = [];

    /// <summary>Creates a node in <paramref name="region"/>.</summary>
    public GeodeNode(string name, string region)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);

        Name = name;
        Region = region;
    }

    /// <summary>What this node is called.</summary>
    public string Name { get; }

    /// <summary>Where it is.</summary>
    public string Region { get; }

    /// <summary>Whether it can serve.</summary>
    public bool Available { get; private set; } = true;

    /// <summary>Whether replication is currently reaching it.</summary>
    public bool CaughtUp { get; private set; } = true;

    /// <summary>How many requests it has answered.</summary>
    public int Served { get; private set; }

    /// <summary>Applies a replicated write, if replication is reaching it.</summary>
    public void Apply(string key, string value)
    {
        if (!CaughtUp)
        {
            return;
        }

        data[key] = value;
    }

    /// <summary>Reads whatever this node currently holds — current or not.</summary>
    public string? Read(string key)
    {
        Served++;
        return data.TryGetValue(key, out string? value) ? value : null;
    }

    /// <summary>Whether this node holds anything for <paramref name="key"/>.</summary>
    public bool Holds(string key) => data.ContainsKey(key);

    /// <summary>Stops replication reaching it, without taking it out of service.</summary>
    public void Lag() => CaughtUp = false;

    /// <summary>Lets replication reach it again.</summary>
    public void CatchUp() => CaughtUp = true;

    /// <summary>Takes it out of service.</summary>
    public void Fail() => Available = false;

    /// <summary>Returns it to service.</summary>
    public void Recover() => Available = true;
}

/// <summary>
/// A set of nodes, geographically distributed, **all holding the same data**.
///
/// A client is served by the nearest available node. If that node is gone,
/// another serves the identical request — because there is no such thing as
/// "this request's node". That ubiquity is what the pattern buys.
///
/// **The price is replication lag.** A node that has not received the latest
/// write serves the previous value, confidently and with no indication that it
/// is doing so. The tests assert that directly, because a model where
/// replication is instant teaches the benefit and none of the cost.
///
/// **What this is not.** Deployment Stamps is the opposite arrangement: there,
/// each copy holds only **its own** tenants' data, a request has exactly one
/// home, and a failed stamp means its tenants are refused rather than served
/// elsewhere. Stamps buy isolation and cannot fail over; geodes buy ubiquity
/// and cannot avoid lag. Sharding partitions data within one deployment, which
/// is neither.
/// </summary>
public sealed class GeodeNetwork
{
    private readonly List<GeodeNode> nodes = [];

    /// <summary>The nodes, in the order they were added.</summary>
    public IReadOnlyList<string> Nodes => [.. nodes.Select(node => node.Name)];

    /// <summary>Adds a node to the network.</summary>
    public void AddNode(GeodeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        nodes.Add(node);
    }

    /// <summary>
    /// Writes, and **replicates to every node**. A node that is lagging simply
    /// does not receive it, which is how the model produces a stale read.
    /// </summary>
    public void Write(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        foreach (GeodeNode node in nodes)
        {
            node.Apply(key, value);
        }
    }

    /// <summary>
    /// How many nodes could answer for <paramref name="key"/> — the benefit, as
    /// a number. In a stamped arrangement this would be one.
    /// </summary>
    public int NodesThatCouldServe(string key) =>
        nodes.Count(node => node.Available && node.Holds(key));

    /// <summary>The nearest available node to <paramref name="from"/>, or any available one.</summary>
    public GeodeNode? Nearest(ClientLocation from)
    {
        foreach (GeodeNode node in nodes)
        {
            if (node.Available && string.Equals(node.Region, from.Region, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return nodes.FirstOrDefault(node => node.Available);
    }

    /// <summary>Reads from the nearest available node, naming which one answered.</summary>
    public GeodeResponse Read(ClientLocation from, string key)
    {
        GeodeNode? node = Nearest(from);

        if (node is null)
        {
            return new GeodeResponse(string.Empty, null, Found: false);
        }

        string? value = node.Read(key);
        return new GeodeResponse(node.Name, value, value is not null);
    }
}
