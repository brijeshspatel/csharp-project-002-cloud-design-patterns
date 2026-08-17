namespace Gatekeeper;

/// <summary>One thing a member of the public sent in.</summary>
/// <param name="Reference">What it claims to be about.</param>
/// <param name="Payload">What it contains.</param>
public readonly record struct Submission(string Reference, string Payload);

/// <summary>What the gatekeeper decided, and why.</summary>
/// <param name="Accepted">Whether it crossed the boundary.</param>
/// <param name="Reason">Why not, where it did not — specific, not "rejected".</param>
/// <param name="Sanitised">What was actually forwarded, where it was.</param>
public readonly record struct ValidationOutcome(bool Accepted, string Reason, Submission Sanitised);

/// <summary>
/// The private side. It holds the credential, it touches the data, and **it is
/// not reachable from outside** — in production by network policy, here by
/// there being no path to it except through the gatekeeper.
///
/// It is deliberately trusting. Its input has already been validated and
/// sanitised, and duplicating those checks here would mean the gatekeeper had
/// bought nothing.
/// </summary>
public sealed class PrivateProcessor
{
    private readonly string credential;
    private readonly List<string> secretsHeld = [];

    /// <summary>Creates a processor holding <paramref name="credential"/>.</summary>
    public PrivateProcessor(string credential)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credential);
        this.credential = credential;
        secretsHeld.Add("database credential");
    }

    /// <summary>
    /// What this component holds that an attacker would want. Non-empty here,
    /// and empty on the exposed side — that asymmetry is the pattern.
    /// </summary>
    public IReadOnlyList<string> SecretsHeld => secretsHeld;

    /// <summary>How many submissions actually reached it.</summary>
    public int Received { get; private set; }

    /// <summary>The last thing it was given, for inspection.</summary>
    public Submission LastReceived { get; private set; }

    /// <summary>Does the real work, using the credential only it has.</summary>
    public string Process(Submission submission)
    {
        Received++;
        LastReceived = submission;
        return $"{submission.Reference} stored using {credential.Split(';')[0]}";
    }
}

/// <summary>
/// The gatekeeper: the only thing exposed to the public, and **it holds no
/// secret at all**.
///
/// That is the whole pattern, and it is a claim about blast radius rather than
/// about duplicated code. An attacker who fully compromises this component
/// gains the ability to submit well-formed claim forms. They do not gain the
/// database credential, because it is not here — and they cannot reach the
/// component that has it except by going through validation.
///
/// It validates **and sanitises**: validation decides yes or no, sanitising
/// changes what crosses the boundary, so the private side never has to be
/// defensive about input the gatekeeper already handled.
///
/// **What this is not.** Gateway Offloading also refuses requests at the edge,
/// and its subject is duplicated code — its services are perfectly reachable
/// and simply have less in them. Here reachability *is* the subject: the
/// private side is unreachable and holds what matters. Gateway Routing decides
/// which service; Gateway Aggregation calls several; neither makes a claim about
/// what an attacker gets.
/// </summary>
public sealed class PublicEndpoint
{
    private const int MaximumPayload = 100;

    private readonly PrivateProcessor processor;

    // Empty, and it stays empty. Held as a field rather than returned as a
    // literal so that "what does the exposed component hold?" is a question
    // about this object's state rather than about a compile-time constant.
    private readonly List<string> secretsHeld = [];

    /// <summary>Creates a gatekeeper in front of <paramref name="processor"/>.</summary>
    public PublicEndpoint(PrivateProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        this.processor = processor;
    }

    /// <summary>
    /// What this component holds that an attacker would want: **nothing**. It
    /// is the exposed half of the pattern and that is exactly why.
    /// </summary>
    public IReadOnlyList<string> SecretsHeld => secretsHeld;

    /// <summary>How many submissions were refused here.</summary>
    public int Refused { get; private set; }

    /// <summary>
    /// Validates, sanitises, and forwards — or refuses, in which case the
    /// private side never learns the submission existed.
    /// </summary>
    public ValidationOutcome Accept(Submission submission)
    {
        if (!submission.Reference.StartsWith("SUB-", StringComparison.Ordinal))
        {
            return Refuse("the reference is not in the expected form");
        }

        if (string.IsNullOrWhiteSpace(submission.Payload))
        {
            return Refuse("the payload is empty");
        }

        if (submission.Payload.Length > MaximumPayload)
        {
            return Refuse($"the payload is too long: {submission.Payload.Length} characters");
        }

        Submission sanitised = submission with { Payload = Sanitise(submission.Payload) };
        processor.Process(sanitised);

        return new ValidationOutcome(Accepted: true, Reason: string.Empty, sanitised);
    }

    private ValidationOutcome Refuse(string reason)
    {
        Refused++;
        return new ValidationOutcome(Accepted: false, reason, default);
    }

    /// <summary>
    /// Removes what the private side should never see. Crude here, and the
    /// principle is what matters: sanitising happens once, at the boundary,
    /// rather than in every component that later handles the value.
    /// </summary>
    private static string Sanitise(string payload)
    {
        // Elements are removed **whole** — tag, content and closing tag.
        // Stripping only the angle brackets would leave `alert(1)` behind as
        // text, which is the half-sanitising that makes a private side think it
        // is safe when it is not.
        int guard = 0;
        int open = payload.IndexOf('<');

        while (open >= 0)
        {
            Assert(++guard <= MaximumPayload, "sanitising did not terminate");

            int close = payload.IndexOf('>', open);
            if (close < 0)
            {
                // An unterminated tag: nothing after it can be trusted.
                return payload.Remove(open);
            }

            string name = payload[(open + 1)..close].Split(' ')[0].TrimStart('/');
            string closing = $"</{name}>";
            int closingAt = payload.IndexOf(closing, close, StringComparison.OrdinalIgnoreCase);
            int removeTo = closingAt >= 0 ? closingAt + closing.Length : close + 1;

            payload = payload.Remove(open, removeTo - open);
            open = payload.IndexOf('<');
        }

        return payload;
    }

    private static void Assert(bool condition, string because)
    {
        if (!condition)
        {
            throw new InvalidOperationException(because);
        }
    }
}
