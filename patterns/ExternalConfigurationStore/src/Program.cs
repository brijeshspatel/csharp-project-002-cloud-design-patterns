using ExternalConfigurationStore;

// Four instances reading their settings from one place outside the deployment
// package. A change lands everywhere with no restarts - and so does a mistake.

Console.WriteLine("External Configuration Store - one place, no redeployment");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

Dictionary<string, string> packaged = new()
{
    ["page-size"] = "20",
};

ConfigurationStore store = new();
store.Set("feature.new-checkout", "off");

List<ApplicationInstance> fleet = [];
foreach (string name in new[] { "web-1", "web-2", "web-3", "web-4" })
{
    fleet.Add(new ApplicationInstance(name, store, packaged));
    store.Register(name);
}

Console.WriteLine("Four instances, reading one store");
Console.WriteLine(new string('-', 62));
Report();

Console.WriteLine();
Console.WriteLine("The flag is turned on - once");
Console.WriteLine(new string('-', 62));

store.Set("feature.new-checkout", "on");
Report();

Console.WriteLine();
Console.WriteLine($"  restarts required: {fleet.Sum(instance => instance.Restarts)}");
Console.WriteLine($"  restarts avoided:  {store.RestartsAvoided}");
Console.WriteLine("  No deployment, no restart, no instance remembering what it was");
Console.WriteLine("  told at startup.");

Console.WriteLine();
Console.WriteLine("Now a mistake, by the same mechanism");
Console.WriteLine(new string('-', 62));

store.Set("feature.new-checkout", "obviously-wrong");
Report();

Console.WriteLine();
Console.WriteLine("  The same speed, the whole fleet. A store that propagates good");
Console.WriteLine("  changes in seconds propagates bad ones in seconds - which is why");
Console.WriteLine("  configuration deserves review, staged rollout and a way back.");

store.Set("feature.new-checkout", "on");

Console.WriteLine();
Console.WriteLine("A setting the store does not hold");
Console.WriteLine(new string('-', 62));
Console.WriteLine($"  page-size:        {fleet[0].Read("page-size")}  (packaged default)");
Console.WriteLine($"  nothing-anywhere: {fleet[0].Read("nothing-anywhere") ?? "nothing"}");
Console.WriteLine();
Console.WriteLine("  The package keeps defaults deliberately. An application that");
Console.WriteLine("  cannot start without reaching the store has made a configuration");
Console.WriteLine("  outage into a total outage.");

Console.WriteLine();
Console.WriteLine("The change history the store keeps");
Console.WriteLine(new string('-', 62));
foreach (SettingChange change in store.History)
{
    Console.WriteLine($"  v{change.Version}  {change.Key} = {change.Value}");
}

void Report()
{
    foreach (ApplicationInstance instance in fleet)
    {
        // Read first: the version an instance reports is the one its last
        // read observed, which is what makes a stalled instance visible.
        string? value = instance.Read("feature.new-checkout");
        Console.WriteLine($"  {instance.Name}  v{instance.ConfigurationVersion}  " +
                          $"feature.new-checkout = {value}");
    }
}
