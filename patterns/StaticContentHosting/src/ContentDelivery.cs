namespace StaticContentHosting;

/// <summary>One request for one path.</summary>
/// <param name="Path">What was asked for.</param>
public readonly record struct PageRequest(string Path);

/// <summary>
/// The application: the thing that renders pages, holds sessions, talks to the
/// database, and costs money per request.
///
/// **It counts the requests it handles**, and that counter is the
/// demonstration. In memory returning a string from a dictionary costs the same
/// whoever does it, so the saving is only visible as the requests this never
/// saw.
/// </summary>
public sealed class ApplicationServer
{
    /// <summary>How many requests reached the application.</summary>
    public int RequestsHandled { get; private set; }

    /// <summary>Renders a page. Real work: the reason the application exists.</summary>
    public string Render(PageRequest request)
    {
        RequestsHandled++;
        return $"rendered: {request.Path}";
    }
}

/// <summary>
/// The delivery front: static assets served from storage, everything else
/// passed to the application.
///
/// A stylesheet is identical for every user and every request. Serving it
/// through the application spends a rendering process, a connection and a slice
/// of capacity on handing back bytes that never change — and does it for every
/// asset on every page load, which is where most requests actually are.
///
/// **A missing asset is answered here, not forwarded.** Falling through to the
/// application on a miss turns a broken link into origin load, which is exactly
/// the load the pattern exists to remove — and it does so under the traffic
/// that is most likely to be hostile.
/// </summary>
public sealed class ContentDelivery
{
    private readonly ApplicationServer application;
    private readonly Dictionary<string, string> assets = [];

    /// <summary>Creates a front over <paramref name="application"/>.</summary>
    public ContentDelivery(ApplicationServer application)
    {
        ArgumentNullException.ThrowIfNull(application);
        this.application = application;
    }

    /// <summary>How many assets are published.</summary>
    public int Assets => assets.Count;

    /// <summary>
    /// Publishes an asset at a path. **Versioned paths are how a far-future
    /// cache lifetime stays safe**: a changed asset is published at a new path
    /// rather than replacing what caches already hold.
    /// </summary>
    public void Publish(string path, string content) => assets[path] = content;

    /// <summary>
    /// Serves a request: from storage where the path is a published asset,
    /// from the application otherwise, and as not-found where an asset path is
    /// unknown.
    /// </summary>
    public string? Serve(PageRequest request)
    {
        if (assets.TryGetValue(request.Path, out string? content))
        {
            return content;
        }

        if (IsAssetPath(request.Path))
        {
            return null;
        }

        return application.Render(request);
    }

    private static bool IsAssetPath(string path) =>
        path.StartsWith("/assets/", StringComparison.Ordinal);
}
