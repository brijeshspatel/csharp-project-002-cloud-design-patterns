using IdempotentConsumer;

// A payment capture whose acknowledgement was lost, so the broker redelivers it.
// Without de-duplication the account is debited twice; with it, once. Then the
// harder case: work that failed must not be recorded as done.

Console.WriteLine("Idempotent Consumer - a redelivery with no second effect");
Console.WriteLine(new string('=', 58));
Console.WriteLine();

PaymentCapture capture = new("MSG-4471", "ACC-42", 25.00m);

Console.WriteLine("Without de-duplication: the same capture arrives twice");
Console.WriteLine(new string('-', 58));

Ledger naive = new();
naive.Debit(capture);
naive.Debit(capture);
Console.WriteLine($"  ACC-42 debited {naive.DebitedFrom("ACC-42"):C} - the customer paid twice");

Console.WriteLine();
Console.WriteLine("With an idempotent handler: the same capture arrives twice");
Console.WriteLine(new string('-', 58));

Ledger ledger = new();
InMemoryProcessedMessageLog log = new();
IdempotentHandler handler = new(log);

Report(handler.Handle(capture, ledger.Debit));
Report(handler.Handle(capture, ledger.Debit));
Console.WriteLine($"  ACC-42 debited {ledger.DebitedFrom("ACC-42"):C} - charged once");

Console.WriteLine();
Console.WriteLine("The case that is easy to get wrong: work that failed");
Console.WriteLine(new string('-', 58));

PaymentCapture failing = new("MSG-4472", "ACC-99", 80.00m);

try
{
    handler.Handle(failing, _ => throw new InvalidOperationException("ledger unavailable"));
}
catch (InvalidOperationException failure)
{
    Console.WriteLine($"  attempt 1 failed: {failure.Message}");
}

Console.WriteLine($"  recorded as processed? {log.HasProcessed("MSG-4472")}");
Console.WriteLine("  so the broker's redelivery will be worked, not suppressed");

Report(handler.Handle(failing, ledger.Debit));
Console.WriteLine($"  ACC-99 debited {ledger.DebitedFrom("ACC-99"):C}");

Console.WriteLine();
Console.WriteLine("Recording before doing the work would have lost that payment");
Console.WriteLine("silently - suppressed as a duplicate, and never debited at all.");

static void Report(HandlingResult result) =>
    Console.WriteLine($"  {result.Outcome,-9} {result.Effect}");
