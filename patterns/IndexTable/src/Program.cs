using IndexTable;

// Customers keyed by id, queried by email. The store counts the records it
// examines, because in memory the saving is otherwise invisible.

Console.WriteLine("Index Table - counting the records a lookup examines");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

const int CustomerCount = 100;
const int Queries = 20;

CustomerStore store = new();
for (int i = 1; i <= CustomerCount; i++)
{
    store.Add(new Customer($"C-{i:000}", $"customer{i:000}@example.test", $"Customer {i}"));
}

Console.WriteLine($"{store.Count} customers keyed by id, {Queries} lookups by email");
Console.WriteLine();

Console.WriteLine("Answering by scanning the store every time");
Console.WriteLine(new string('-', 60));
for (int i = 0; i < Queries; i++)
{
    string wanted = $"customer{(i * 5) + 1:000}@example.test";
    _ = store.Scan().Count(customer => customer.Email == wanted);
}

Console.WriteLine($"  records examined: {store.RecordsExamined}");

Console.WriteLine();
Console.WriteLine("Answering through an index table");
Console.WriteLine(new string('-', 60));

CustomerStore indexed = new();
for (int i = 1; i <= CustomerCount; i++)
{
    indexed.Add(new Customer($"C-{i:000}", $"customer{i:000}@example.test", $"Customer {i}"));
}

EmailIndex index = new(indexed);
foreach (Customer customer in indexed.Scan())
{
    index.Add(customer.Email, customer.Id);
}

int afterBuild = indexed.RecordsExamined;

for (int i = 0; i < Queries; i++)
{
    index.TryFind($"customer{(i * 5) + 1:000}@example.test", out _);
}

Console.WriteLine($"  records examined to build the index: {afterBuild}");
Console.WriteLine($"  records examined by {Queries} lookups:    {indexed.RecordsExamined - afterBuild}");
Console.WriteLine($"  total: {indexed.RecordsExamined} against {store.RecordsExamined}");

Console.WriteLine();
Console.WriteLine("The duty: the index is maintained, not informed");
Console.WriteLine(new string('-', 60));

indexed.Add(new Customer("C-999", "new@example.test", "New Customer"));
Console.WriteLine($"  customer added, index not updated: found = {index.TryFind("new@example.test", out _)}");

index.Add("new@example.test", "C-999");
Console.WriteLine($"  index updated:                     found = {index.TryFind("new@example.test", out _)}");

Console.WriteLine();
Console.WriteLine("An index table holds the key, not the data: it is derived,");
Console.WriteLine("cheap to rebuild, and only as truthful as its upkeep.");
