namespace IdempotentConsumer;

/// <summary>
/// Where the money actually moves.
///
/// It exists so the pattern's claim is observable rather than asserted: a
/// duplicate capture must leave the balance unchanged, and only a real side
/// effect can show that.
/// </summary>
public sealed class Ledger
{
    private readonly Dictionary<string, decimal> debited = [];

    /// <summary>How much has been taken from <paramref name="account"/> in total.</summary>
    public decimal DebitedFrom(string account) =>
        debited.TryGetValue(account, out decimal amount) ? amount : 0m;

    /// <summary>Takes <paramref name="capture"/>'s amount from its account.</summary>
    /// <returns>A description of what happened, which the handler records.</returns>
    public string Debit(PaymentCapture capture)
    {
        debited[capture.Account] = DebitedFrom(capture.Account) + capture.Amount;
        return $"debited {capture.Amount:C} from {capture.Account}";
    }
}
