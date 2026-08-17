using System.Text;

namespace ClaimCheck;

/// <summary>Thrown when a claim check refers to a payload that is no longer there.</summary>
public sealed class PayloadUnavailableException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public PayloadUnavailableException()
        : base("The payload for this claim check is no longer available.")
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>.</summary>
    public PayloadUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and a cause.</summary>
    public PayloadUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The token that stands in for a payload on the bus.</summary>
public static class ClaimCheckToken
{
    /// <summary>What marks a message body as a claim check rather than a payload.</summary>
    public const string Prefix = "claim-check:";

    /// <summary>Builds a token for <paramref name="reference"/>.</summary>
    public static string For(string reference) => Prefix + reference;

    /// <summary>Reads the store reference out of a body, if it is a token.</summary>
    public static bool TryRead(string body, out string reference)
    {
        if (body.StartsWith(Prefix, StringComparison.Ordinal))
        {
            reference = body[Prefix.Length..];
            return true;
        }

        reference = string.Empty;
        return false;
    }
}

/// <summary>Where large payloads actually live.</summary>
public interface IPayloadStore
{
    /// <summary>Stores <paramref name="payload"/> and returns its reference.</summary>
    string Put(string payload);

    /// <summary>Fetches a payload by reference.</summary>
    /// <exception cref="PayloadUnavailableException">It is not there.</exception>
    string Retrieve(string reference);
}

/// <summary>
/// An in-memory store. A real one is blob storage with a lifecycle policy — and
/// that policy is what makes <see cref="CollectAll"/> worth modelling.
/// </summary>
public sealed class InMemoryPayloadStore : IPayloadStore
{
    private readonly Dictionary<string, string> payloads = [];
    private int next;

    /// <summary>How many payloads are stored.</summary>
    public int Count => payloads.Count;

    /// <inheritdoc/>
    public string Put(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        string reference = $"blob-{++next:0000}";
        payloads[reference] = payload;
        return reference;
    }

    /// <inheritdoc/>
    public string Retrieve(string reference) =>
        payloads.TryGetValue(reference, out string? payload)
            ? payload
            : throw new PayloadUnavailableException(
                $"'{reference}' is not in the store; it may have expired");

    /// <summary>
    /// Deletes everything, standing in for a retention policy expiring payloads.
    ///
    /// The message and its payload have **independent lifetimes**, and that is
    /// the pattern's sharpest operational hazard: a message can outlive the
    /// thing it points at.
    /// </summary>
    public void CollectAll() => payloads.Clear();

    /// <summary>How many bytes a payload occupies.</summary>
    public static int SizeOf(string payload) => Encoding.UTF8.GetByteCount(payload);
}
