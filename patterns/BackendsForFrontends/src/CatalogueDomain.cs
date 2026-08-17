namespace BackendsForFrontends;

/// <summary>
/// A product as the domain holds it — everything anybody might need, in the
/// shape the business thinks in rather than the shape any screen wants.
/// </summary>
/// <param name="Sku">What identifies it.</param>
/// <param name="Name">Its full name.</param>
/// <param name="Price">What it costs.</param>
/// <param name="Description">Prose, for a page that has room.</param>
/// <param name="Images">Every image.</param>
/// <param name="Reviews">What customers said.</param>
/// <param name="Dimensions">How big it is.</param>
/// <param name="Warranty">How long it is covered.</param>
public readonly record struct Product(
    string Sku,
    string Name,
    decimal Price,
    string Description,
    IReadOnlyList<string> Images,
    IReadOnlyList<string> Reviews,
    string Dimensions,
    string Warranty);

/// <summary>
/// The one source of product truth. **Both backends read it and neither owns
/// it** — which is what separates this pattern from two teams quietly building
/// two catalogues that drift.
/// </summary>
public sealed class CatalogueDomain
{
    private readonly Dictionary<string, Product> products = [];

    /// <summary>How many times a backend has asked it something.</summary>
    public int Queries { get; private set; }

    /// <summary>How many products it holds.</summary>
    public int Count => products.Count;

    /// <summary>Adds a product.</summary>
    public void Add(Product product) => products[product.Sku] = product;

    /// <summary>Finds a product, or nothing.</summary>
    public Product? Find(string sku)
    {
        Queries++;
        return products.TryGetValue(sku, out Product product) ? product : null;
    }
}

/// <summary>What a phone gets: enough for a list row, and nothing more.</summary>
/// <param name="Sku">What identifies it.</param>
/// <param name="Name">Truncated to fit a narrow screen.</param>
/// <param name="Price">What it costs.</param>
/// <param name="Thumbnail">One image, not three.</param>
public readonly record struct MobileView(string Sku, string Name, decimal Price, string Thumbnail);

/// <summary>What a desktop page gets: everything the page has room to show.</summary>
/// <param name="Sku">What identifies it.</param>
/// <param name="Name">In full.</param>
/// <param name="Price">What it costs.</param>
/// <param name="Description">The prose.</param>
/// <param name="Images">All of them.</param>
/// <param name="Reviews">All of them.</param>
/// <param name="Dimensions">How big it is.</param>
/// <param name="Warranty">How long it is covered.</param>
public readonly record struct DesktopView(
    string Sku,
    string Name,
    decimal Price,
    string Description,
    IReadOnlyList<string> Images,
    IReadOnlyList<string> Reviews,
    string Dimensions,
    string Warranty);

/// <summary>
/// The backend the mobile team owns.
///
/// It exists because a phone rendering a list on a mobile network wants four
/// fields, not eight — and because deciding that is a **client concern**. A
/// shared endpoint would make the truncation length a negotiation between two
/// client teams and whoever owns the endpoint; here it is a constructor
/// argument the mobile team changes when its layout changes.
/// </summary>
public sealed class MobileBackend
{
    private readonly CatalogueDomain domain;
    private readonly int nameLimit;

    /// <summary>Creates the mobile backend over <paramref name="domain"/>.</summary>
    public MobileBackend(CatalogueDomain domain, int nameLimit)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nameLimit);

        this.domain = domain;
        this.nameLimit = nameLimit;
    }

    /// <summary>How many fields a mobile response carries.</summary>
    public static int FieldsPerResponse => 4;

    /// <summary>The compact shape, or nothing.</summary>
    public MobileView? Get(string sku)
    {
        if (domain.Find(sku) is not { } product)
        {
            return null;
        }

        return new MobileView(
            product.Sku,
            Shorten(product.Name, nameLimit),
            product.Price,
            product.Images.Count > 0 ? product.Images[0] : string.Empty);
    }

    private static string Shorten(string name, int limit) =>
        name.Length <= limit ? name : $"{name[..limit]}...";
}

/// <summary>
/// The backend the desktop team owns.
///
/// It returns everything, because a product page has room for everything. That
/// is not a different amount of the same decision — it is a different decision,
/// made by a different team, for a different screen.
/// </summary>
public sealed class DesktopBackend
{
    private readonly CatalogueDomain domain;

    /// <summary>Creates the desktop backend over <paramref name="domain"/>.</summary>
    public DesktopBackend(CatalogueDomain domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        this.domain = domain;
    }

    /// <summary>How many fields a desktop response carries.</summary>
    public static int FieldsPerResponse => 8;

    /// <summary>The full shape, or nothing.</summary>
    public DesktopView? Get(string sku)
    {
        if (domain.Find(sku) is not { } product)
        {
            return null;
        }

        return new DesktopView(
            product.Sku,
            product.Name,
            product.Price,
            product.Description,
            product.Images,
            product.Reviews,
            product.Dimensions,
            product.Warranty);
    }
}
