namespace PipesAndFilters.Tests;

/// <summary>
/// What pipes and filters guarantees: that a complex transformation is a
/// sequence of independent stages, **each usable on its own and reusable in
/// more than one pipeline**.
///
/// Reuse is asserted rather than claimed. A single fixed pipeline demonstrates
/// decomposition; two pipelines sharing filter instances demonstrate that the
/// stages are genuinely independent of the arrangement they sit in.
/// </summary>
public class PipelineTests
{
    private const string RawLine = "2026-08-17 ERROR disk full for ada@example.test";

    private static LogEntry Incoming(string raw) => new(raw, string.Empty, string.Empty, string.Empty, string.Empty);

    [Fact]
    public void Passes_an_entry_through_every_filter_in_order()
    {
        Pipeline pipeline = new(
            new ParseFilter(), new RedactFilter(), new EnrichFilter("web-01"), new FormatFilter());

        LogEntry? result = pipeline.Run(Incoming(RawLine));

        // The order is asserted through the outcome. Formatting before parsing
        // would format an empty entry; redacting before parsing would have
        // nothing to redact.
        Assert.Equal("[ERROR] disk full for [redacted] (web-01)", result?.Formatted);
    }

    [Fact]
    public void Builds_a_second_pipeline_from_the_same_filters()
    {
        ParseFilter parse = new();
        RedactFilter redact = new();
        FormatFilter format = new();

        Pipeline ingestion = new(parse, redact, new EnrichFilter("web-01"), format);
        Pipeline audit = new(parse, redact, format);

        LogEntry? ingested = ingestion.Run(Incoming(RawLine));
        LogEntry? audited = audit.Run(Incoming(RawLine));

        // Same three filter instances, two arrangements, two outputs. Neither
        // pipeline knows the other exists.
        Assert.Equal("[ERROR] disk full for [redacted] (web-01)", ingested?.Formatted);
        Assert.Equal("[ERROR] disk full for [redacted] ()", audited?.Formatted);
    }

    [Fact]
    public void Drops_an_entry_a_filter_rejects()
    {
        Pipeline pipeline = new(new ParseFilter(), new FormatFilter());

        IReadOnlyList<LogEntry> surviving = pipeline.RunAll(
            [Incoming(RawLine), Incoming("not a log line at all"), Incoming(RawLine)]);

        // A filter that rejects removes the entry from the stream rather than
        // failing the batch. Two of three survive.
        Assert.Equal(2, surviving.Count);
    }

    [Fact]
    public void Leaves_each_filter_independent_of_the_others()
    {
        RedactFilter redact = new();

        // No pipeline, no parse step, no arrangement of any kind — the filter is
        // a unit that happens to be usable in one.
        LogEntry? result = redact.Apply(
            new LogEntry(string.Empty, "INFO", "mail ada@example.test now", string.Empty, string.Empty));

        Assert.Equal("mail [redacted] now", result?.Message);
    }

    [Fact]
    public void Reports_the_filters_a_pipeline_is_composed_of()
    {
        Pipeline pipeline = new(
            new ParseFilter(), new RedactFilter(), new EnrichFilter("web-01"), new FormatFilter());

        Assert.Equal(["parse", "redact", "enrich", "format"], pipeline.Composition);
    }
}
