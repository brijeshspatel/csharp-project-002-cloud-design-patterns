using EventSourcing;

// An account whose balance is nowhere stored: it is what the events add up to.

Console.WriteLine("Event Sourcing - the events are the record, state is derived");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

const string Account = "ACC-4417";

EventStore store = new();
store.Append(Account, new AccountEvent("opened", 0m, 1), expectedVersion: 0);
store.Append(Account, new AccountEvent("deposited", 500m, 2), expectedVersion: 1);
store.Append(Account, new AccountEvent("withdrawn", -120m, 3), expectedVersion: 2);
store.Append(Account, new AccountEvent("deposited", 75m, 4), expectedVersion: 3);

Console.WriteLine("The stream, oldest first");
Console.WriteLine(new string('-', 62));
foreach (AccountEvent change in store.Stream(Account))
{
    Console.WriteLine($"  v{change.Version}  {change.Kind,-10} {change.Delta,10:+0.00;-0.00;0.00}");
}

Console.WriteLine();
Console.WriteLine("State, derived by replay");
Console.WriteLine(new string('-', 62));

AccountAggregate now = AccountAggregate.Replay(store.Stream(Account));
Console.WriteLine($"  balance at v{now.Version}: {now.Balance:0.00}");

Console.WriteLine();
Console.WriteLine("Any past state is a prefix of the same stream");
Console.WriteLine(new string('-', 62));

for (int version = 1; version <= store.VersionOf(Account); version++)
{
    AccountAggregate past = AccountAggregate.Replay(store.StreamAsAt(Account, version));
    Console.WriteLine($"  balance at v{past.Version}: {past.Balance,8:0.00}");
}

Console.WriteLine();
Console.WriteLine("A writer working from stale state is refused");
Console.WriteLine(new string('-', 62));

try
{
    store.Append(Account, new AccountEvent("withdrawn", -400m, 5), expectedVersion: 3);
}
catch (InvalidOperationException refusal)
{
    Console.WriteLine($"  {refusal.Message}");
}

Console.WriteLine();
Console.WriteLine($"  events still held: {store.Stream(Account).Count}");
Console.WriteLine();
Console.WriteLine("Nothing was updated and nothing was deleted. The balance is not");
Console.WriteLine("stored anywhere; it is what the events add up to.");
