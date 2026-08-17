using DeploymentStamps;

// Six tenants across three independent copies of the application. The
// interesting moment is when one copy fails.

Console.WriteLine("Deployment Stamps - one stamp fails, and most tenants never know");
Console.WriteLine(new string('=', 66));
Console.WriteLine();

string[] tenants = ["acme", "brightly", "cobalt", "delta", "everest", "foxtrot"];

StampRouter router = new();
Stamp one = new("stamp-1");
Stamp two = new("stamp-2");
Stamp three = new("stamp-3");
router.AddStamp(one);
router.AddStamp(two);
router.AddStamp(three);

foreach (string tenant in tenants)
{
    router.Assign(tenant);
}

Console.WriteLine("Where each tenant lives");
Console.WriteLine(new string('-', 66));
foreach (string tenant in tenants)
{
    Console.WriteLine($"  {tenant,-10} -> {router.HomeOf(tenant)?.Name}");
}

Console.WriteLine();
Console.WriteLine("  Each stamp is a complete copy, including its own data store.");
Console.WriteLine("  No stamp knows the others exist.");

Console.WriteLine();
Console.WriteLine("Everything working");
Console.WriteLine(new string('-', 66));
Serve();

Console.WriteLine();
Console.WriteLine($"{two.Name} fails");
Console.WriteLine(new string('-', 66));
two.Fail();
Serve();

Console.WriteLine();
Console.WriteLine($"  tenants on {two.Name}:        {two.Tenants.Count}");
Console.WriteLine($"  tenants unaffected:        {router.TenantsUnaffectedBy(two)}");
Console.WriteLine();
Console.WriteLine("  Its tenants are refused rather than served elsewhere. Another");
Console.WriteLine("  stamp holds none of their data, so failing over would answer");
Console.WriteLine("  with somebody else's world.");

Console.WriteLine();
Console.WriteLine("A fourth stamp is added, and nobody moves");
Console.WriteLine(new string('-', 66));

two.Recover();
router.AddStamp(new Stamp("stamp-4"));
router.Assign("gamma");

foreach (string tenant in tenants)
{
    Console.WriteLine($"  {tenant,-10} -> {router.HomeOf(tenant)?.Name}");
}

Console.WriteLine($"  {"gamma",-10} -> {router.HomeOf("gamma")?.Name}   (new)");
Console.WriteLine();
Console.WriteLine("  Scale comes from adding stamps, not from making one bigger -");
Console.WriteLine("  and adding one migrates nobody, because migration would mean");
Console.WriteLine("  moving a data store.");

void Serve()
{
    foreach (string tenant in tenants)
    {
        string? answer = router.Send(new TenantRequest(tenant, "an invoice"));
        Console.WriteLine($"  {tenant,-10} {answer ?? "refused: its stamp is down"}");
    }
}
