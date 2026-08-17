using Geode;

// Three nodes in three regions, all holding the same data. Any node can serve
// any request - and the price of that is a node serving an old value.

Console.WriteLine("Geode - every node holds everything");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

GeodeNode europe = new("geode-eu", "europe");
GeodeNode asia = new("geode-ap", "asia");
GeodeNode america = new("geode-us", "america");

GeodeNetwork network = new();
network.AddNode(europe);
network.AddNode(asia);
network.AddNode(america);
network.Write("catalogue/sku-77", "Standing Desk");

string[] regions = ["europe", "asia", "america"];

Console.WriteLine("Each client is served by its nearest node");
Console.WriteLine(new string('-', 60));
Read();
Console.WriteLine();
Console.WriteLine($"  nodes that could have served any of those: {network.NodesThatCouldServe("catalogue/sku-77")}");
Console.WriteLine("  In a stamped arrangement that number would be one, because no");
Console.WriteLine("  other copy would hold the data at all.");

Console.WriteLine();
Console.WriteLine("The nearest node to Asia is lost");
Console.WriteLine(new string('-', 60));
asia.Fail();
Read();
Console.WriteLine();
Console.WriteLine("  A different node, the same answer. Nothing was refused, because");
Console.WriteLine("  there is no such thing as this request's node.");

asia.Recover();

Console.WriteLine();
Console.WriteLine("The price: Asia stops receiving replication, and a write lands");
Console.WriteLine(new string('-', 60));

asia.Lag();
network.Write("catalogue/sku-77", "Standing Desk, Walnut");
Read();

Console.WriteLine();
Console.WriteLine("  Asia is serving the old value, confidently, with nothing in the");
Console.WriteLine("  response to say so. Every node holding everything is exactly why.");

Console.WriteLine();
Console.WriteLine("Asia catches up");
Console.WriteLine(new string('-', 60));

asia.CatchUp();
network.Write("catalogue/sku-77", "Standing Desk, Walnut");
Read();

void Read()
{
    foreach (string region in regions)
    {
        GeodeResponse response = network.Read(new ClientLocation(region), "catalogue/sku-77");
        Console.WriteLine($"  client in {region,-8} -> {response.ServedBy,-9} says '{response.Value}'");
    }
}
