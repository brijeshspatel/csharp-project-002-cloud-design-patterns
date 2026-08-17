namespace AntiCorruptionLayer;

/// <summary>
/// An order as the mainframe holds it: fixed-width fields, abbreviated names,
/// amounts in pence, dates as digits, and a single-letter status code.
///
/// None of that is a caricature — it is what a system written in 1987 and never
/// rewritten actually looks like, and every part of it is a thing the modern
/// model must not acquire.
/// </summary>
/// <param name="OrderNumber">Ten digits, zero-padded: `ORDNO`.</param>
/// <param name="CustomerNumber">A letter and four digits: `CUSTNO`.</param>
/// <param name="AmountInPence">Pence, as an integer: `AMTPENCE`.</param>
/// <param name="StatusCode">One letter: `STATCD`.</param>
/// <param name="OrderDate">Eight digits, `yyyyMMdd`: `DTEORD`.</param>
public readonly record struct LegacyOrderRecord(
    string OrderNumber,
    string CustomerNumber,
    int AmountInPence,
    string StatusCode,
    string OrderDate);

/// <summary>
/// An order as the modern system thinks about one: readable references, money
/// as money, a date as a date, and a status a person can say out loud.
///
/// **Nothing here is shaped by the mainframe**, which is the point of the
/// pattern rather than a nicety.
/// </summary>
/// <param name="Reference">`ORD-1042`.</param>
/// <param name="CustomerId">`CUST-7`.</param>
/// <param name="Amount">Pounds.</param>
/// <param name="PlacedOn">A date.</param>
/// <param name="Status">placed, shipped or cancelled.</param>
public readonly record struct ModernOrder(
    string Reference,
    string CustomerId,
    decimal Amount,
    string Status,
    DateOnly PlacedOn);

/// <summary>
/// The legacy system. It is not going to change, and the pattern exists
/// precisely because asking it to is not an option.
/// </summary>
public sealed class LegacyMainframe
{
    private readonly Dictionary<string, LegacyOrderRecord> records = [];
    private readonly List<LegacyOrderRecord> posted = [];

    /// <summary>Everything written back to it, in order.</summary>
    public IReadOnlyList<LegacyOrderRecord> Posted => posted;

    /// <summary>Loads a record into the mainframe.</summary>
    public void Load(LegacyOrderRecord record) => records[record.OrderNumber] = record;

    /// <summary>Reads a record by its padded order number.</summary>
    public LegacyOrderRecord? Fetch(string orderNumber) =>
        records.TryGetValue(orderNumber, out LegacyOrderRecord record) ? record : null;

    /// <summary>Writes a record back.</summary>
    public void Post(LegacyOrderRecord record)
    {
        records[record.OrderNumber] = record;
        posted.Add(record);
    }
}

/// <summary>
/// The boundary. Everything the mainframe knows stops here.
///
/// It translates **both ways**, deliberately: a layer that only translates
/// inward leaves every write path reaching around it, and within a year the
/// modern code is constructing padded order numbers itself. Both directions or
/// neither.
///
/// **Where the models do not align, the layer decides — and records the
/// decision.** The mainframe's "held in the overnight batch" has no modern
/// equivalent, so it becomes `placed` and the concession is written down. That
/// list is the honest artefact of the pattern: a translation with no
/// concessions is either trivial or lying.
///
/// **What this is not.** Strangler Fig is a migration strategy — routing that
/// shifts feature by feature until the legacy system is empty. This is a
/// boundary, and it may stand for a decade with no migration planned. The two
/// are commonly used together and neither requires the other.
/// </summary>
public sealed class OrderTranslator
{
    private readonly LegacyMainframe mainframe;
    private readonly List<string> concessions = [];

    /// <summary>Creates a translator over <paramref name="mainframe"/>.</summary>
    public OrderTranslator(LegacyMainframe mainframe)
    {
        ArgumentNullException.ThrowIfNull(mainframe);
        this.mainframe = mainframe;
    }

    /// <summary>
    /// Decisions the layer had to make because the two models disagree. Empty
    /// is suspicious; a real boundary always has some.
    /// </summary>
    public IReadOnlyList<string> Concessions => concessions;

    /// <summary>Reads an order from the mainframe, in modern terms.</summary>
    public ModernOrder? Read(string reference)
    {
        string orderNumber = ToOrderNumber(reference);
        return mainframe.Fetch(orderNumber) is { } record ? ToModern(record) : null;
    }

    /// <summary>Writes a modern order back to the mainframe, in its terms.</summary>
    public void Write(ModernOrder order) => mainframe.Post(ToLegacy(order));

    /// <summary>Translates a legacy record into the modern model.</summary>
    public ModernOrder ToModern(LegacyOrderRecord record)
    {
        return new ModernOrder(
            $"ORD-{int.Parse(record.OrderNumber, System.Globalization.CultureInfo.InvariantCulture)}",
            $"CUST-{int.Parse(record.CustomerNumber[1..], System.Globalization.CultureInfo.InvariantCulture)}",
            record.AmountInPence / 100m,
            ToModernStatus(record.StatusCode),
            DateOnly.ParseExact(record.OrderDate, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Translates a modern order into the legacy shape.</summary>
    public LegacyOrderRecord ToLegacy(ModernOrder order)
    {
        return new LegacyOrderRecord(
            ToOrderNumber(order.Reference),
            $"C{order.CustomerId["CUST-".Length..].PadLeft(4, '0')}",
            (int)(order.Amount * 100m),
            ToLegacyStatus(order.Status),
            order.PlacedOn.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string ToOrderNumber(string reference) =>
        reference["ORD-".Length..].PadLeft(10, '0');

    private string ToModernStatus(string statusCode)
    {
        switch (statusCode)
        {
            case "A":
                return "placed";

            case "S":
                return "shipped";

            case "X":
                return "cancelled";

            case "B":
                // The mainframe distinguishes "accepted" from "accepted but held
                // in tonight's batch". The modern model has no concept of the
                // mainframe's batch schedule and must not acquire one, so the
                // distinction is dropped — visibly.
                Concede("legacy status 'B' (held in the overnight batch) has no modern equivalent; treated as placed");
                return "placed";

            default:
                Concede($"legacy status '{statusCode}' is unknown; treated as placed");
                return "placed";
        }
    }

    private string ToLegacyStatus(string status)
    {
        switch (status)
        {
            case "placed":
                return "A";

            case "shipped":
                return "S";

            case "cancelled":
                return "X";

            default:
                // The modern model has grown a status the mainframe cannot
                // express. Losing it silently is how a write-back corrupts the
                // legacy system's own reporting.
                Concede($"modern status '{status}' has no legacy code; written as 'A'");
                return "A";
        }
    }

    private void Concede(string note)
    {
        if (!concessions.Contains(note))
        {
            concessions.Add(note);
        }
    }
}
