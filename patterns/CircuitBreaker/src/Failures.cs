namespace CircuitBreaker;

/// <summary>
/// A dependency failure the breaker counts.
///
/// The breaker trips on this type and nothing else, and that restraint is
/// deliberate. A breaker that counted every exception would open on a validation
/// error in the caller's own code — cutting off a healthy dependency because the
/// caller had a bug, which is a worse outage than the one it was protecting
/// against.
/// </summary>
public sealed class DependencyFailureException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public DependencyFailureException()
        : base("The dependency failed.")
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>.</summary>
    public DependencyFailureException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and a cause.</summary>
    public DependencyFailureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown instead of calling a dependency the breaker has cut off.
///
/// The caller gets this immediately, which is the entire point: a fast, clear
/// refusal instead of a slow timeout on a service already known to be down.
/// </summary>
public sealed class CircuitOpenException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public CircuitOpenException()
        : base("The circuit is open; the call was not attempted.")
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>.</summary>
    public CircuitOpenException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and a cause.</summary>
    public CircuitOpenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
