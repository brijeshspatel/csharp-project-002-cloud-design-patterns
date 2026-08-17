using BackendsForFrontends;

// One product, one domain, two clients that want genuinely different things -
// and two backends owned by the teams that own those clients.

Console.WriteLine("Backends for Frontends - one domain, two shapes");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

CatalogueDomain domain = new();
domain.Add(new Product(
    "SKU-1042",
    "Professional Ergonomic Standing Desk, Walnut",
    549.00m,
    "A height-adjustable desk with a walnut veneer top and a memory controller.",
    ["desk-front.jpg", "desk-side.jpg", "desk-detail.jpg"],
    ["Solid build", "Arrived early"],
    "160 x 80 x 62-128 cm",
    "5 years"));

MobileBackend mobile = new(domain, nameLimit: 20);
DesktopBackend desktop = new(domain);

Console.WriteLine("The mobile client asks");
Console.WriteLine(new string('-', 60));

MobileView? phone = mobile.Get("SKU-1042");
Console.WriteLine($"  name:      {phone?.Name}");
Console.WriteLine($"  price:     {phone?.Price:0.00}");
Console.WriteLine($"  thumbnail: {phone?.Thumbnail}");
Console.WriteLine($"  fields:    {MobileBackend.FieldsPerResponse}");

Console.WriteLine();
Console.WriteLine("The desktop client asks, about the same product");
Console.WriteLine(new string('-', 60));

DesktopView? page = desktop.Get("SKU-1042");
Console.WriteLine($"  name:        {page?.Name}");
Console.WriteLine($"  description: {page?.Description}");
Console.WriteLine($"  images:      {string.Join(", ", page?.Images ?? [])}");
Console.WriteLine($"  reviews:     {string.Join("; ", page?.Reviews ?? [])}");
Console.WriteLine($"  dimensions:  {page?.Dimensions}");
Console.WriteLine($"  warranty:    {page?.Warranty}");
Console.WriteLine($"  fields:      {DesktopBackend.FieldsPerResponse}");

Console.WriteLine();
Console.WriteLine("One domain underneath both");
Console.WriteLine(new string('-', 60));
Console.WriteLine($"  products held:   {domain.Count}");
Console.WriteLine($"  domain queries:  {domain.Queries}");
Console.WriteLine("  The backends shape a response. Neither owns the data, so there");
Console.WriteLine("  is no second catalogue to keep in step.");

Console.WriteLine();
Console.WriteLine("The mobile team ships a change; the desktop team does not notice");
Console.WriteLine(new string('-', 60));

string desktopBefore = page?.Name ?? string.Empty;
MobileBackend narrower = new(domain, nameLimit: 10);

Console.WriteLine($"  mobile, new list layout:  {narrower.Get("SKU-1042")?.Name}");
Console.WriteLine($"  desktop, unchanged:       {desktop.Get("SKU-1042")?.Name}");
Console.WriteLine($"  desktop shape moved:      {desktopBefore != desktop.Get("SKU-1042")?.Name}");

Console.WriteLine();
Console.WriteLine("A single shared endpoint would have made that truncation length a");
Console.WriteLine("negotiation between two client teams and whoever owned the endpoint.");
Console.WriteLine("Here it is a constructor argument owned by the team that needs it.");
