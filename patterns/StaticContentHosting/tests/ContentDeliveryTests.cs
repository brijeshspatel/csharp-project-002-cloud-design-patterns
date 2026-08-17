namespace StaticContentHosting.Tests;

/// <summary>
/// What static content hosting guarantees: that a file which never changes per
/// request is served **without the application being involved at all**.
///
/// Asserted against <see cref="ApplicationServer.RequestsHandled"/>. In memory
/// serving bytes from a dictionary costs the same either way, so the saving is
/// only visible as a count of the requests the application did not have to
/// handle.
/// </summary>
public class ContentDeliveryTests
{
    private static ContentDelivery DeliveryWithAssets(ApplicationServer application)
    {
        ContentDelivery delivery = new(application);
        delivery.Publish("/assets/site-v3.css", "body { margin: 0 }");
        delivery.Publish("/assets/app-v3.js", "console.log('hello')");
        delivery.Publish("/assets/logo.png", "PNG-BYTES");
        return delivery;
    }

    [Fact]
    public void Serves_a_dynamic_page_from_the_application()
    {
        ApplicationServer application = new();
        ContentDelivery delivery = DeliveryWithAssets(application);

        string? body = delivery.Serve(new PageRequest("/orders/1042"));

        Assert.Equal("rendered: /orders/1042", body);
        Assert.Equal(1, application.RequestsHandled);
    }

    [Fact]
    public void Serves_a_static_asset_without_touching_the_application()
    {
        ApplicationServer application = new();
        ContentDelivery delivery = DeliveryWithAssets(application);

        string? body = delivery.Serve(new PageRequest("/assets/site-v3.css"));

        Assert.Equal("body { margin: 0 }", body);

        // Zero. That is the entire benefit, and it is what routing static
        // assets through the application quietly destroys.
        Assert.Equal(0, application.RequestsHandled);
    }

    [Fact]
    public void Counts_every_application_request()
    {
        ApplicationServer application = new();
        ContentDelivery delivery = DeliveryWithAssets(application);

        // One page load: the document, then three assets referenced by it.
        delivery.Serve(new PageRequest("/orders/1042"));
        delivery.Serve(new PageRequest("/assets/site-v3.css"));
        delivery.Serve(new PageRequest("/assets/app-v3.js"));
        delivery.Serve(new PageRequest("/assets/logo.png"));

        // Four requests, one of which needed the application.
        Assert.Equal(1, application.RequestsHandled);
    }

    [Fact]
    public void Reports_a_missing_asset()
    {
        ApplicationServer application = new();
        ContentDelivery delivery = DeliveryWithAssets(application);

        string? body = delivery.Serve(new PageRequest("/assets/absent-v3.css"));

        // Not found, and still not the application's problem: an unpublished
        // asset must not fall through to a request the origin has to handle.
        Assert.Null(body);
        Assert.Equal(0, application.RequestsHandled);
    }

    [Fact]
    public void Serves_a_versioned_asset_path()
    {
        ApplicationServer application = new();
        ContentDelivery delivery = DeliveryWithAssets(application);

        delivery.Publish("/assets/site-v4.css", "body { margin: 8px }");

        // Both versions are live at once under distinct paths, which is what
        // makes a far-future cache lifetime safe.
        Assert.Equal("body { margin: 0 }", delivery.Serve(new PageRequest("/assets/site-v3.css")));
        Assert.Equal("body { margin: 8px }", delivery.Serve(new PageRequest("/assets/site-v4.css")));
        Assert.Equal(0, application.RequestsHandled);
    }
}
