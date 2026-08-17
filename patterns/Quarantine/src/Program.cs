using Quarantine;

// Four third-party packages submitted for use. Three are refused before the
// build can see them, and the registry holds only what passed.

Console.WriteLine("Quarantine - checked before the workload can consume it");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

AssetRegistry registry = new();
QuarantineGate gate = new(registry, ["MIT", "Apache-2.0", "BSD-3-Clause"]);

Console.WriteLine($"  checks applied: {string.Join(", ", gate.Checks)}");
Console.WriteLine();

ExternalAsset[] submitted =
[
    new("serilog", "3.1.1", "Apache-2.0", HasKnownVulnerability: false, Scanned: true),
    new("left-pad", "1.3.0", "MIT", HasKnownVulnerability: true, Scanned: true),
    new("some-gpl-thing", "2.0.0", "GPL-3.0", HasKnownVulnerability: false, Scanned: true),
    new("mystery-lib", "0.9.0", "MIT", HasKnownVulnerability: false, Scanned: false),
];

Console.WriteLine("Submissions");
Console.WriteLine(new string('-', 62));
foreach (ExternalAsset asset in submitted)
{
    AssetVerdict verdict = gate.Submit(asset);
    Console.WriteLine($"  {asset.Name,-16} {(verdict.Admitted ? "admitted" : "refused")}");

    if (!verdict.Admitted)
    {
        Console.WriteLine($"    {verdict.Reason}");
    }
}

Console.WriteLine();
Console.WriteLine("What the workload can actually use");
Console.WriteLine(new string('-', 62));
foreach (string name in registry.Admitted)
{
    Console.WriteLine($"  {name}");
}

Console.WriteLine();
Console.WriteLine($"  admitted: {gate.AdmittedCount}");
Console.WriteLine($"  refused before use: {gate.Refused}");

Console.WriteLine();
Console.WriteLine("The registry is the only thing downstream reads, which is what");
Console.WriteLine("makes this a gate rather than a report: nothing has to remember");
Console.WriteLine("to check a verdict before using something.");

Console.WriteLine();
Console.WriteLine("Note 'mystery-lib'. Nothing was found in it because nothing looked.");
Console.WriteLine("A gate that admitted it on those grounds would have inverted its");
Console.WriteLine("own purpose: unchecked is not the same as clean.");
