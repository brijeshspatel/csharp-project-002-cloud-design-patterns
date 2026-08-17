namespace MaterializedView.Tests;

/// <summary>
/// What a materialised view guarantees: that a query is answered **without
/// touching the source**, and that the price of it is an answer which is only as
/// current as the last rebuild.
///
/// Both are asserted against <see cref="OrderSource.RowsScanned"/>. In memory an
/// aggregation over a few rows is free, so the saving is only visible as a
/// count.
/// </summary>
public class SalesByRegionViewTests
{
    private static OrderSource SourceWithOrders()
    {
        OrderSource source = new();
        source.Add(new OrderRow("ORD-1", "north", 100m));
        source.Add(new OrderRow("ORD-2", "south", 250m));
        source.Add(new OrderRow("ORD-3", "north", 50m));
        source.Add(new OrderRow("ORD-4", "south", 25m));
        return source;
    }

    [Fact]
    public void Answers_without_scanning_the_source()
    {
        OrderSource source = SourceWithOrders();
        SalesByRegionView view = new(source);
        view.Rebuild();

        int afterBuild = source.RowsScanned;
        view.TotalFor("north");
        view.TotalFor("south");
        view.TotalFor("north");

        // Three queries, no further scanning. That is the entire benefit, and
        // nothing else here would distinguish a view from a live aggregation.
        Assert.Equal(afterBuild, source.RowsScanned);
    }

    [Fact]
    public void Scanning_the_source_examines_every_row()
    {
        OrderSource source = SourceWithOrders();

        decimal total = source.Scan()
            .Where(order => order.Region == "north")
            .Sum(order => order.Amount);

        Assert.Equal(150m, total);
        Assert.Equal(4, source.RowsScanned);
    }

    [Fact]
    public void Aggregates_orders_by_region()
    {
        SalesByRegionView view = new(SourceWithOrders());
        view.Rebuild();

        RegionTotal? north = view.TotalFor("north");

        Assert.Equal(150m, north?.Total);
        Assert.Equal(2, north?.Orders);
    }

    [Fact]
    public void Reflects_new_orders_only_after_a_rebuild()
    {
        OrderSource source = SourceWithOrders();
        SalesByRegionView view = new(source);
        view.Rebuild();

        source.Add(new OrderRow("ORD-5", "north", 400m));

        // Still 150. The view does not know the source changed, and serving the
        // old figure is the pattern working as designed.
        Assert.Equal(150m, view.TotalFor("north")?.Total);

        view.Rebuild();

        Assert.Equal(550m, view.TotalFor("north")?.Total);
    }

    [Fact]
    public void Reports_nothing_for_a_region_with_no_orders()
    {
        SalesByRegionView view = new(SourceWithOrders());
        view.Rebuild();

        Assert.Null(view.TotalFor("west"));
    }
}
