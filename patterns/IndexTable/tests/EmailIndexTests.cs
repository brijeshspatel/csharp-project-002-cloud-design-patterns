namespace IndexTable.Tests;

/// <summary>
/// What an index table guarantees: that a lookup by a field the store is not
/// keyed on examines **one record instead of all of them**, and that the index
/// stays useful only while it is maintained alongside the data.
///
/// Both are asserted against <see cref="CustomerStore.RecordsExamined"/>. In
/// memory a scan over a handful of records is free, so the saving is only
/// visible as a count.
/// </summary>
public class EmailIndexTests
{
    private static CustomerStore StoreWithCustomers()
    {
        CustomerStore store = new();
        store.Add(new Customer("C-1", "ada@example.test", "Ada"));
        store.Add(new Customer("C-2", "ben@example.test", "Ben"));
        store.Add(new Customer("C-3", "cas@example.test", "Cas"));
        store.Add(new Customer("C-4", "dee@example.test", "Dee"));
        return store;
    }

    private static EmailIndex IndexOver(CustomerStore store)
    {
        // Entries added directly rather than by scanning the store, so the
        // one-time build cost does not pollute RecordsExamined in the asserts.
        EmailIndex index = new(store);
        index.Add("ada@example.test", "C-1");
        index.Add("ben@example.test", "C-2");
        index.Add("cas@example.test", "C-3");
        index.Add("dee@example.test", "C-4");
        return index;
    }

    [Fact]
    public void Finds_a_customer_by_an_indexed_field()
    {
        CustomerStore store = StoreWithCustomers();
        EmailIndex index = IndexOver(store);

        bool found = index.TryFind("cas@example.test", out Customer customer);

        Assert.True(found);
        Assert.Equal("C-3", customer.Id);

        // One record examined: the one the index pointed at. This count is the
        // entire benefit, and it is what a scan disguised as a lookup breaks.
        Assert.Equal(1, store.RecordsExamined);
    }

    [Fact]
    public void Scanning_examines_every_record()
    {
        CustomerStore store = StoreWithCustomers();

        Customer? found = null;
        foreach (Customer customer in store.Scan())
        {
            if (customer.Email == "cas@example.test")
            {
                found = customer;
            }
        }

        Assert.Equal("C-3", found?.Id);
        Assert.Equal(4, store.RecordsExamined);
    }

    [Fact]
    public void Adds_a_new_customer_to_the_index()
    {
        CustomerStore store = StoreWithCustomers();
        EmailIndex index = IndexOver(store);

        store.Add(new Customer("C-5", "eve@example.test", "Eve"));
        index.Add("eve@example.test", "C-5");

        Assert.True(index.TryFind("eve@example.test", out Customer customer));
        Assert.Equal("C-5", customer.Id);
    }

    [Fact]
    public void Removes_a_deleted_customer_from_the_index()
    {
        CustomerStore store = StoreWithCustomers();
        EmailIndex index = IndexOver(store);

        store.Remove("C-2");
        index.Remove("ben@example.test");

        Assert.False(index.TryFind("ben@example.test", out _));
    }

    [Fact]
    public void Reports_nothing_for_an_unknown_email()
    {
        CustomerStore store = StoreWithCustomers();
        EmailIndex index = IndexOver(store);

        Assert.False(index.TryFind("nobody@example.test", out _));
        Assert.Equal(0, store.RecordsExamined);
    }
}
