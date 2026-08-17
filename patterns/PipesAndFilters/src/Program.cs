using PipesAndFilters;

// Log ingestion in four stages - and then the same stages, rearranged, feeding
// somewhere else. The reuse is the point; one pipeline would only show the
// decomposition.

Console.WriteLine("Pipes and Filters - the same stages, two pipelines");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

ParseFilter parse = new();
RedactFilter redact = new();
EnrichFilter enrich = new("web-01");
FormatFilter format = new();

LogEntry[] incoming =
[
    Raw("2026-08-17 ERROR disk full for ada@example.test"),
    Raw("2026-08-17 WARN retry scheduled for ben@example.test"),
    Raw("not a log line at all"),
    Raw("2026-08-17 INFO healthy"),
];

Pipeline ingestion = new(parse, redact, enrich, format);
Pipeline audit = new(parse, redact, format);

Console.WriteLine($"  ingestion pipeline: {string.Join(" -> ", ingestion.Composition)}");
Console.WriteLine($"  audit pipeline:     {string.Join(" -> ", audit.Composition)}");
Console.WriteLine();
Console.WriteLine("  Three filter instances appear in both. Neither pipeline knows");
Console.WriteLine("  the other exists, and no filter knows it is in a pipeline.");

Console.WriteLine();
Console.WriteLine($"{incoming.Length} lines in, through the ingestion pipeline");
Console.WriteLine(new string('-', 60));
foreach (LogEntry entry in ingestion.RunAll(incoming))
{
    Console.WriteLine($"  {entry.Formatted}");
}

Console.WriteLine();
Console.WriteLine("The same lines, through the audit pipeline");
Console.WriteLine(new string('-', 60));
foreach (LogEntry entry in audit.RunAll(incoming))
{
    Console.WriteLine($"  {entry.Formatted}");
}

Console.WriteLine();
Console.WriteLine("One line was dropped by both");
Console.WriteLine(new string('-', 60));
Console.WriteLine($"  in: {incoming.Length}, out: {ingestion.RunAll(incoming).Count}");
Console.WriteLine("  \"not a log line at all\" was rejected by parse. A filter that");
Console.WriteLine("  rejects removes the entry from the stream; it does not fail the");
Console.WriteLine("  batch, and the stages after it never see it.");

Console.WriteLine();
Console.WriteLine("A filter on its own, with no pipeline at all");
Console.WriteLine(new string('-', 60));
LogEntry? redactedAlone = redact.Apply(
    new LogEntry(string.Empty, "INFO", "mail cas@example.test today", string.Empty, string.Empty));
Console.WriteLine($"  {redactedAlone?.Message}");
Console.WriteLine("  Nothing about a filter depends on the arrangement it sits in,");
Console.WriteLine("  which is exactly why a second arrangement costs nothing.");

static LogEntry Raw(string line) =>
    new(line, string.Empty, string.Empty, string.Empty, string.Empty);
