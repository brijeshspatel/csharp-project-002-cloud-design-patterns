using StaticContentHosting;

// A page load is one document and many assets. The application counts the
// requests it handles, because in memory the saving is otherwise invisible.

Console.WriteLine("Static Content Hosting - counting the requests the application never saw");
Console.WriteLine(new string('=', 72));
Console.WriteLine();

const int PageLoads = 50;

string[] assetPaths =
[
    "/assets/site-v3.css",
    "/assets/app-v3.js",
    "/assets/logo.png",
    "/assets/hero-v2.jpg",
];

Console.WriteLine("Everything through the application");
Console.WriteLine(new string('-', 72));

ApplicationServer everything = new();
for (int load = 0; load < PageLoads; load++)
{
    everything.Render(new PageRequest("/orders/1042"));
    foreach (string asset in assetPaths)
    {
        everything.Render(new PageRequest(asset));
    }
}

Console.WriteLine($"  {PageLoads} page loads, {assetPaths.Length} assets each");
Console.WriteLine($"  application requests: {everything.RequestsHandled}");

Console.WriteLine();
Console.WriteLine("Static assets served from storage");
Console.WriteLine(new string('-', 72));

ApplicationServer application = new();
ContentDelivery delivery = new(application);
delivery.Publish("/assets/site-v3.css", "body { margin: 0 }");
delivery.Publish("/assets/app-v3.js", "console.log('hello')");
delivery.Publish("/assets/logo.png", "PNG-BYTES");
delivery.Publish("/assets/hero-v2.jpg", "JPEG-BYTES");

for (int load = 0; load < PageLoads; load++)
{
    delivery.Serve(new PageRequest("/orders/1042"));
    foreach (string asset in assetPaths)
    {
        delivery.Serve(new PageRequest(asset));
    }
}

Console.WriteLine($"  {delivery.Assets} assets published");
Console.WriteLine($"  application requests: {application.RequestsHandled} against {everything.RequestsHandled}");

Console.WriteLine();
Console.WriteLine("A missing asset is answered here, not forwarded");
Console.WriteLine(new string('-', 72));

int before = application.RequestsHandled;
string? missing = delivery.Serve(new PageRequest("/assets/absent-v9.css"));

Console.WriteLine($"  /assets/absent-v9.css -> {missing ?? "not found"}");
Console.WriteLine($"  application requests added: {application.RequestsHandled - before}");

Console.WriteLine();
Console.WriteLine("A new version is a new path, so caches never have to be told");
Console.WriteLine(new string('-', 72));

delivery.Publish("/assets/site-v4.css", "body { margin: 8px }");
Console.WriteLine($"  /assets/site-v3.css -> {delivery.Serve(new PageRequest("/assets/site-v3.css"))}");
Console.WriteLine($"  /assets/site-v4.css -> {delivery.Serve(new PageRequest("/assets/site-v4.css"))}");

Console.WriteLine();
Console.WriteLine("Most requests on a page load are for bytes that never change.");
Console.WriteLine("Serving them from storage is capacity the application keeps.");
