using Gatekeeper;

// A public submission endpoint in front of a private processor that holds a
// database credential. The interesting number is how many hostile submissions
// reached the private side.

Console.WriteLine("Gatekeeper - the exposed half holds nothing worth stealing");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

PrivateProcessor processor = new("Server=private;Password=hunter2");
PublicEndpoint endpoint = new(processor);

Console.WriteLine("What each component holds");
Console.WriteLine(new string('-', 62));
Console.WriteLine($"  public endpoint:   {(endpoint.SecretsHeld.Count == 0 ? "nothing" : string.Join(", ", endpoint.SecretsHeld))}");
Console.WriteLine($"  private processor: {string.Join(", ", processor.SecretsHeld)}");
Console.WriteLine();
Console.WriteLine("  An attacker who fully compromises the exposed component gains");
Console.WriteLine("  the ability to submit well-formed claim forms. Not the credential.");

Console.WriteLine();
Console.WriteLine("A valid submission");
Console.WriteLine(new string('-', 62));

ValidationOutcome accepted = endpoint.Accept(new Submission("SUB-1042", "a claim form"));
Console.WriteLine($"  accepted: {accepted.Accepted}");
Console.WriteLine($"  the processor stored: {processor.LastReceived.Reference}");

Console.WriteLine();
Console.WriteLine("A submission carrying markup");
Console.WriteLine(new string('-', 62));

endpoint.Accept(new Submission("SUB-1043", "a claim<script>alert(1)</script> form"));
Console.WriteLine($"  sent by the client:      a claim<script>alert(1)</script> form");
Console.WriteLine($"  seen by the processor:   {processor.LastReceived.Payload}");
Console.WriteLine();
Console.WriteLine("  Validation says yes or no. Sanitising changes what crosses the");
Console.WriteLine("  boundary, so the private side never has to be defensive about");
Console.WriteLine("  input the gatekeeper already handled.");

Console.WriteLine();
Console.WriteLine("Three submissions that should never be processed");
Console.WriteLine(new string('-', 62));

int before = processor.Received;

foreach (Submission bad in new[]
{
    new Submission("nonsense", "a claim form"),
    new Submission("SUB-1", string.Empty),
    new Submission("SUB-2", new string('x', 500)),
})
{
    ValidationOutcome outcome = endpoint.Accept(bad);
    Console.WriteLine($"  refused: {outcome.Reason}");
}

Console.WriteLine();
Console.WriteLine($"  submissions that reached the private side: {processor.Received - before}");
Console.WriteLine($"  refused at the gatekeeper:                 {endpoint.Refused}");

Console.WriteLine();
Console.WriteLine("That zero is the guarantee. Not that bad input is reported - that");
Console.WriteLine("it is never processed, and that the component which could be");
Console.WriteLine("reached had nothing an attacker wanted.");
