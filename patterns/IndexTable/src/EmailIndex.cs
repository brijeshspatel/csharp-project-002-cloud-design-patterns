namespace IndexTable;

/// <summary>One customer, as the store holds it.</summary>
/// <param name="Id">The key the store is partitioned on.</param>
/// <param name="Email">A field queries want that the store is not keyed on.</param>
/// <param name="Name">Who they are.</param>
public readonly record struct Customer(string Id, string Email, string Name);

/// <summary>
/// The primary store: customers keyed by id, because that is how they are
/// written and how they are almost always read.
///
/// **It counts the records it examines**, and that counter is the
/// demonstration. In memory a scan over a handful of records is free, so an
/// index that merely answers faster proves nothing.
/// </summary>
public sealed class CustomerStore
{
    private readonly Dictionary<string, Customer> customers = [];

    /// <summary>How many records have been examined by reads.</summary>
    public int RecordsExamined { get; private set; }

    /// <summary>How many customers the store holds.</summary>
    public int Count => customers.Count;

    /// <summary>Adds or replaces a customer under its id.</summary>
    public void Add(Customer customer) => customers[customer.Id] = customer;

    /// <summary>Removes a customer by id.</summary>
    public void Remove(string id) => customers.Remove(id);

    /// <summary>A keyed read: examines exactly the record it fetches.</summary>
    public Customer? GetById(string id)
    {
        if (!customers.TryGetValue(id, out Customer customer))
        {
            return null;
        }

        RecordsExamined++;
        return customer;
    }

    /// <summary>Reads every record, counting each one.</summary>
    public IEnumerable<Customer> Scan()
    {
        foreach (Customer customer in customers.Values)
        {
            RecordsExamined++;
            yield return customer;
        }
    }
}

/// <summary>
/// An index table: a second, small structure keyed on the field queries
/// actually ask by, holding **the primary key rather than a copy of the data**.
///
/// The store answers "who is customer C-3?" in one step and "who has this
/// email?" only by examining every record. The index turns the second question
/// into two keyed reads: email to id here, id to record in the store.
///
/// **The index is only as good as its maintenance.** It is not told when the
/// data changes; every add and delete must update it too, and a missed update
/// leaves it pointing at nothing — or worse, not pointing at something that
/// exists. That duty is the running cost of the pattern, and the tests state it
/// as calls the caller must make.
/// </summary>
public sealed class EmailIndex
{
    private readonly CustomerStore store;
    private readonly Dictionary<string, string> idsByEmail = [];

    /// <summary>Creates an index resolving into <paramref name="store"/>.</summary>
    public EmailIndex(CustomerStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>How many entries the index holds.</summary>
    public int Count => idsByEmail.Count;

    /// <summary>Records that <paramref name="email"/> belongs to <paramref name="id"/>.</summary>
    public void Add(string email, string id) => idsByEmail[email] = id;

    /// <summary>Forgets an email. Part of every delete, or the index lies.</summary>
    public void Remove(string email) => idsByEmail.Remove(email);

    /// <summary>
    /// Finds the customer holding <paramref name="email"/>, examining only the
    /// record the index points at — never the rest of the store.
    /// </summary>
    public bool TryFind(string email, out Customer customer)
    {
        customer = default;

        if (!idsByEmail.TryGetValue(email, out string? id))
        {
            return false;
        }

        Customer? found = store.GetById(id);
        if (found is null)
        {
            return false;
        }

        customer = found.Value;
        return true;
    }
}
