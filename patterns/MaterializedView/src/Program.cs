using MaterializedView;

// Sales aggregated by region. The source counts the rows it hands out, because
// in memory the saving is otherwise invisible.

Console.WriteLine("Materialized View - counting the rows the view did not scan");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

const int OrderCount = 500;
const int Queries = 20;

OrderSource live = new();
string[] regions = ["north", "south", "east", "west"];
for (int i = 1; i <= OrderCount; i++)
{
    live.Add(new OrderRow($"ORD-{i:0000}", regions[i % regions.Length], 10m + (i % 40)));
}

Console.WriteLine($"{live.Count} orders, {Queries} queries for one region's total");
Console.WriteLine();

Console.WriteLine("Answering from the source every time");
Console.WriteLine(new string('-', 60));
for (int i = 0; i < Queries; i++)
{
    _ = live.Scan().Where(o => o.Region == "north").Sum(o => o.Amount);
}

Console.WriteLine($"  rows scanned: {live.RowsScanned}");

Console.WriteLine();
Console.WriteLine("Answering from a materialised view");
Console.WriteLine(new string('-', 60));

OrderSource viewed = new();
for (int i = 1; i <= OrderCount; i++)
{
    viewed.Add(new OrderRow($"ORD-{i:0000}", regions[i % regions.Length], 10m + (i % 40)));
}

SalesByRegionView view = new(viewed);
view.Rebuild();
int afterBuild = viewed.RowsScanned;

for (int i = 0; i < Queries; i++)
{
    view.TotalFor("north");
}

Console.WriteLine($"  rows scanned to build the view: {afterBuild}");
Console.WriteLine($"  rows scanned by {Queries} queries:    {viewed.RowsScanned - afterBuild}");
Console.WriteLine($"  total: {viewed.RowsScanned} against {live.RowsScanned}");

Console.WriteLine();
Console.WriteLine("The cost: the view does not know the source changed");
Console.WriteLine(new string('-', 60));

RegionTotal? before = view.TotalFor("north");
viewed.Add(new OrderRow("ORD-9999", "north", 5000m));

Console.WriteLine($"  north total, before the new order: {before?.Total:0.00}");
Console.WriteLine($"  north total, after it was added:   {view.TotalFor("north")?.Total:0.00}  <- stale");

view.Rebuild();
Console.WriteLine($"  north total, after a rebuild:      {view.TotalFor("north")?.Total:0.00}");

Console.WriteLine();
Console.WriteLine("A view is derived data and always disposable: losing it costs");
Console.WriteLine("a rebuild, never data.");
