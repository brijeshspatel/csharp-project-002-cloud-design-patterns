namespace BackendsForFrontends.Tests;

/// <summary>
/// What backends for frontends guarantees: that two clients whose needs
/// **genuinely differ** each get a backend shaped for them, over one shared
/// domain — and that changing one cannot affect the other.
///
/// The last point is the whole justification. Two backends serving identical
/// shapes would be duplication; two backends that can be changed independently
/// are what removes the negotiation that a single shared endpoint forces on
/// every client team.
/// </summary>
public class BackendsForFrontendsTests
{
    private const string Sku = "SKU-1042";

    private static CatalogueDomain DomainWithProduct()
    {
        CatalogueDomain domain = new();
        domain.Add(new Product(
            Sku,
            "Professional Ergonomic Standing Desk, Walnut",
            549.00m,
            "A height-adjustable desk with a walnut veneer top and a memory controller.",
            ["desk-front.jpg", "desk-side.jpg", "desk-detail.jpg"],
            ["Solid build", "Arrived early"],
            "160 x 80 x 62-128 cm",
            "5 years"));
        return domain;
    }

    [Fact]
    public void Serves_the_mobile_client_a_compact_shape()
    {
        MobileBackend mobile = new(DomainWithProduct(), nameLimit: 20);

        MobileView? view = mobile.Get(Sku);

        // A truncated name, one image, and no prose. A phone on a mobile network
        // rendering a list does not want a paragraph and three URLs per row.
        Assert.Equal("Professional Ergonom...", view?.Name);
        Assert.Equal("desk-front.jpg", view?.Thumbnail);
        Assert.Equal(4, MobileBackend.FieldsPerResponse);
    }

    [Fact]
    public void Serves_the_desktop_client_the_full_shape()
    {
        DesktopBackend desktop = new(DomainWithProduct());

        DesktopView? view = desktop.Get(Sku);

        Assert.Equal("Professional Ergonomic Standing Desk, Walnut", view?.Name);
        Assert.Equal(3, view?.Images.Count);
        Assert.Equal(2, view?.Reviews.Count);
        Assert.Equal("5 years", view?.Warranty);
        Assert.Equal(8, DesktopBackend.FieldsPerResponse);
    }

    [Fact]
    public void Shares_one_domain_between_both_backends()
    {
        CatalogueDomain domain = DomainWithProduct();
        MobileBackend mobile = new(domain, nameLimit: 20);
        DesktopBackend desktop = new(domain);

        mobile.Get(Sku);
        desktop.Get(Sku);

        // One source of product truth, queried twice. The backends shape a
        // response; they do not own the data, and there is no second catalogue
        // to keep in step.
        Assert.Equal(2, domain.Queries);
        Assert.Equal(1, domain.Count);
    }

    [Fact]
    public void Changes_one_backend_without_affecting_the_other()
    {
        CatalogueDomain domain = DomainWithProduct();
        DesktopBackend desktop = new(domain);
        DesktopView? before = desktop.Get(Sku);

        // The mobile team ships a change: shorter names for a new list layout.
        MobileBackend changed = new(domain, nameLimit: 10);

        Assert.Equal("Profession...", changed.Get(Sku)?.Name);

        // The whole desktop view, compared field for field. Its backend did
        // not change, so nothing about its response may have.
        Assert.Equal(before, desktop.Get(Sku));
    }

    [Fact]
    public void Reports_nothing_for_a_product_that_does_not_exist()
    {
        CatalogueDomain domain = DomainWithProduct();

        Assert.Null(new MobileBackend(domain, nameLimit: 20).Get("SKU-NONE"));
        Assert.Null(new DesktopBackend(domain).Get("SKU-NONE"));
    }
}
