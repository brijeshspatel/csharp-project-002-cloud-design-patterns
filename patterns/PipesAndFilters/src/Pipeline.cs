namespace PipesAndFilters;

/// <summary>
/// One log line as it moves along the pipe.
///
/// Every filter takes one of these and returns one of these, which is what lets
/// them be rearranged: no filter names another, and none knows its position.
/// </summary>
/// <param name="Raw">The line as it arrived.</param>
/// <param name="Level">Its severity, once parsed.</param>
/// <param name="Message">Its text, once parsed.</param>
/// <param name="Source">Where it came from, once enriched.</param>
/// <param name="Formatted">The finished output, once formatted.</param>
public readonly record struct LogEntry(
    string Raw,
    string Level,
    string Message,
    string Source,
    string Formatted);

/// <summary>
/// One stage. It transforms an entry, or **rejects it by returning nothing**.
///
/// The interface is deliberately narrow: same type in, same type out, no
/// context, no pipeline reference, no knowledge of neighbours. That narrowness
/// is the entire source of the pattern's reusability — a filter that needed to
/// know what came before it could only ever be used where that thing came
/// before it.
/// </summary>
public interface IFilter
{
    /// <summary>What this stage is called, for reporting a composition.</summary>
    string Name { get; }

    /// <summary>Transforms the entry, or returns nothing to drop it.</summary>
    LogEntry? Apply(LogEntry entry);
}

/// <summary>Splits a raw line into level and message, rejecting what it cannot read.</summary>
public sealed class ParseFilter : IFilter
{
    /// <inheritdoc />
    public string Name => "parse";

    /// <inheritdoc />
    public LogEntry? Apply(LogEntry entry)
    {
        string[] parts = entry.Raw.Split(' ', 3);

        // Rejecting is a first-class outcome, not an error: a malformed line
        // leaves the stream and the rest of the batch carries on.
        if (parts.Length < 3 || !parts[1].All(char.IsAsciiLetterUpper))
        {
            return null;
        }

        return entry with { Level = parts[1], Message = parts[2] };
    }
}

/// <summary>Removes email addresses from the message.</summary>
public sealed class RedactFilter : IFilter
{
    /// <inheritdoc />
    public string Name => "redact";

    /// <inheritdoc />
    public LogEntry? Apply(LogEntry entry)
    {
        string[] words = entry.Message.Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Contains('@', StringComparison.Ordinal))
            {
                words[i] = "[redacted]";
            }
        }

        return entry with { Message = string.Join(' ', words) };
    }
}

/// <summary>Adds the host the line came from.</summary>
public sealed class EnrichFilter : IFilter
{
    private readonly string source;

    /// <summary>Creates a filter stamping entries with <paramref name="source"/>.</summary>
    public EnrichFilter(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        this.source = source;
    }

    /// <inheritdoc />
    public string Name => "enrich";

    /// <inheritdoc />
    public LogEntry? Apply(LogEntry entry) => entry with { Source = source };
}

/// <summary>Renders the finished line.</summary>
public sealed class FormatFilter : IFilter
{
    /// <inheritdoc />
    public string Name => "format";

    /// <inheritdoc />
    public LogEntry? Apply(LogEntry entry) =>
        entry with { Formatted = $"[{entry.Level}] {entry.Message} ({entry.Source})" };
}

/// <summary>
/// A sequence of filters, and nothing else.
///
/// **The pipeline holds the arrangement; the filters hold the work.** That
/// split is what makes a second pipeline free: the same filter instances can be
/// composed differently, because none of them knows what a pipeline is.
///
/// **What this is not.** The other coordination patterns in this tier concern a
/// multi-step operation across services and what happens when one step fails.
/// This one is about decomposing a single transformation into stages that can be
/// recombined — the coordination is of *processing*, not of distributed
/// commitments.
/// </summary>
public sealed class Pipeline
{
    private readonly IFilter[] filters;

    /// <summary>Composes a pipeline from filters, applied in the order given.</summary>
    public Pipeline(params IFilter[] filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        this.filters = filters;
    }

    /// <summary>The stage names, in order — what this pipeline is made of.</summary>
    public IReadOnlyList<string> Composition =>
        filters.Select(filter => filter.Name).ToList();

    /// <summary>
    /// Puts one entry through every stage, stopping at the first that rejects
    /// it.
    /// </summary>
    public LogEntry? Run(LogEntry entry)
    {
        LogEntry current = entry;

        foreach (IFilter filter in filters)
        {
            LogEntry? next = filter.Apply(current);
            if (next is null)
            {
                return null;
            }

            current = next.Value;
        }

        return current;
    }

    /// <summary>Runs a batch, keeping whatever survived.</summary>
    public IReadOnlyList<LogEntry> RunAll(IEnumerable<LogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        List<LogEntry> surviving = [];

        foreach (LogEntry entry in entries)
        {
            if (Run(entry) is { } result)
            {
                surviving.Add(result);
            }
        }

        return surviving;
    }
}
