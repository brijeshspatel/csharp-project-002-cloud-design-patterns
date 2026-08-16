namespace Retry;

/// <summary>
/// A failure worth trying again.
///
/// The distinction this type draws is the whole pattern. Retrying a transient
/// fault costs a delay and usually succeeds; retrying a genuine defect costs
/// the same delay every time and never succeeds, while hiding the defect behind
/// what looks like slowness. A policy that retries everything is not resilient,
/// it is slow to fail.
/// </summary>
public sealed class TransientFailureException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public TransientFailureException()
        : base("The operation failed transiently.")
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>.</summary>
    public TransientFailureException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and a cause.</summary>
    public TransientFailureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
