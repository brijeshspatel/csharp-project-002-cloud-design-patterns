using StranglerFig;

// Billing migrated from a legacy system one feature at a time. The interesting
// part is the middle, where both systems are serving real traffic.

Console.WriteLine("Strangler Fig - a migration is a process, not an evening");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

string[] features = ["invoice", "refund", "statement", "dunning"];

LegacyBilling legacy = new();
ModernBilling modern = new();
MigrationRouter router = new(legacy, modern, features);

Console.WriteLine("Day one: nothing has moved");
Console.WriteLine(new string('-', 62));
Serve();

foreach (string feature in features)
{
    Console.WriteLine();
    Console.WriteLine($"'{feature}' is reimplemented and the route is switched");
    Console.WriteLine(new string('-', 62));
    router.Migrate(feature);
    Serve();
}

Console.WriteLine();
Console.WriteLine("The legacy system can now be switched off");
Console.WriteLine(new string('-', 62));
Console.WriteLine($"  migration complete:        {router.Complete}");
Console.WriteLine($"  legacy requests this run:  {legacy.Handled}");
Console.WriteLine($"  modern requests this run:  {modern.Handled}");

Console.WriteLine();
Console.WriteLine("A feature nobody wrote down");
Console.WriteLine(new string('-', 62));

string answer = router.Send(new BillingRequest("chargeback", "ACC-1"));
Console.WriteLine($"  chargeback -> {answer}");
Console.WriteLine($"  unknown features seen: {router.UnknownFeatures}");
Console.WriteLine();
Console.WriteLine("  Falling back to the legacy system is the opposite of what a");
Console.WriteLine("  routing gateway should do, and here it is correct: nobody fully");
Console.WriteLine("  understands a system old enough to need replacing. It is counted");
Console.WriteLine("  rather than silent, so a migration whose fallback traffic never");
Console.WriteLine("  falls is visibly not finished - whatever the feature list says.");

void Serve()
{
    foreach (string feature in features)
    {
        Console.WriteLine($"    {feature,-11} -> {router.Send(new BillingRequest(feature, "ACC-1"))}");
    }

    Console.WriteLine($"  migrated: {router.MigratedProportion:P0}" +
                      $"   remaining: {(router.Remaining.Count == 0 ? "none" : string.Join(", ", router.Remaining))}");
}
