namespace Gatekeeper.Tests;

/// <summary>
/// What a gatekeeper guarantees: that a refused request **never reaches the
/// private back end**, and that the exposed component **holds nothing worth
/// stealing**.
///
/// Both are asserted structurally rather than described. The first is a count
/// on the processor; the second is the asymmetry between what each component
/// holds — because the pattern's claim is about blast radius, and a validating
/// proxy that shares the back end's credentials has no blast-radius claim to
/// make.
/// </summary>
public class PublicEndpointTests
{
    private const string Credential = "Server=private;Password=hunter2";

    private static (PublicEndpoint Endpoint, PrivateProcessor Processor) Assemble()
    {
        PrivateProcessor processor = new(Credential);
        return (new PublicEndpoint(processor), processor);
    }

    [Fact]
    public void Forwards_a_valid_submission_to_the_private_processor()
    {
        (PublicEndpoint endpoint, PrivateProcessor processor) = Assemble();

        ValidationOutcome outcome = endpoint.Accept(new Submission("SUB-1042", "a claim form"));

        Assert.True(outcome.Accepted);
        Assert.Equal(1, processor.Received);
    }

    [Fact]
    public void Refuses_a_malformed_submission_before_the_processor()
    {
        (PublicEndpoint endpoint, _) = Assemble();

        ValidationOutcome outcome = endpoint.Accept(new Submission("nonsense", "a claim form"));

        Assert.False(outcome.Accepted);
    }

    [Fact]
    public void Never_reaches_the_processor_for_a_refused_submission()
    {
        (PublicEndpoint endpoint, PrivateProcessor processor) = Assemble();

        endpoint.Accept(new Submission("nonsense", "a claim form"));
        endpoint.Accept(new Submission("SUB-1", string.Empty));
        endpoint.Accept(new Submission("SUB-2", new string('x', 500)));

        // Three hostile or malformed submissions, and the private side never ran
        // once. This count is the pattern's actual guarantee: not that bad input
        // is reported, but that it is never processed.
        Assert.Equal(0, processor.Received);
    }

    [Fact]
    public void Holds_no_credential_of_its_own()
    {
        (PublicEndpoint endpoint, PrivateProcessor processor) = Assemble();

        // The asymmetry is the point. Compromising the exposed component yields
        // nothing; the credential is only on the side that is not reachable.
        Assert.Empty(endpoint.SecretsHeld);
        Assert.NotEmpty(processor.SecretsHeld);
    }

    [Fact]
    public void Sanitises_a_submission_it_forwards()
    {
        (PublicEndpoint endpoint, PrivateProcessor processor) = Assemble();

        endpoint.Accept(new Submission("SUB-1042", "a claim<script>alert(1)</script> form"));

        // Validation says yes or no; sanitising changes what crosses the
        // boundary. The private side should never have to be defensive about
        // input the gatekeeper already handled.
        Assert.Equal("a claim form", processor.LastReceived.Payload);
    }

    [Fact]
    public void Reports_why_a_submission_was_refused()
    {
        (PublicEndpoint endpoint, _) = Assemble();

        ValidationOutcome badReference = endpoint.Accept(new Submission("nonsense", "form"));
        ValidationOutcome empty = endpoint.Accept(new Submission("SUB-1", string.Empty));
        ValidationOutcome tooLong = endpoint.Accept(new Submission("SUB-2", new string('x', 500)));

        // Distinct reasons, because "rejected" tells an operator nothing about
        // whether this is an attack, a client bug or a limit set too low.
        Assert.Contains("reference", badReference.Reason, StringComparison.Ordinal);
        Assert.Contains("empty", empty.Reason, StringComparison.Ordinal);
        Assert.Contains("long", tooLong.Reason, StringComparison.Ordinal);
    }
}
