using Cqrs;

// Commands through one model, queries through another, a projection between.
// The staleness between them is shown, not hidden.

Console.WriteLine("CQRS - one model for deciding, another for asking");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

OrderWriteModel writeModel = new();
OrderReadModel readModel = new();
OrderProjection projection = new(writeModel, readModel);

Console.WriteLine("Three orders arrive as commands");
Console.WriteLine(new string('-', 60));

writeModel.Execute(new PlaceOrder("ORD-1", "CUST-1", 120m));
writeModel.Execute(new PlaceOrder("ORD-2", "CUST-1", 80m));
writeModel.Execute(new PlaceOrder("ORD-3", "CUST-2", 50m));

Console.WriteLine($"  write model holds {writeModel.Count} orders");
Console.WriteLine($"  dashboard for CUST-1: {Describe(readModel.SummaryFor("CUST-1"))}  <- stale");

Console.WriteLine();
Console.WriteLine("The projection runs");
Console.WriteLine(new string('-', 60));

projection.Run();

Console.WriteLine($"  dashboard for CUST-1: {Describe(readModel.SummaryFor("CUST-1"))}");
Console.WriteLine($"  dashboard for CUST-2: {Describe(readModel.SummaryFor("CUST-2"))}");

Console.WriteLine();
Console.WriteLine("A query sent to the write model is refused");
Console.WriteLine(new string('-', 60));

try
{
    writeModel.SummaryFor("CUST-1");
}
catch (NotSupportedException refusal)
{
    Console.WriteLine($"  {refusal.Message}");
}

Console.WriteLine();
Console.WriteLine("Another order lands; the dashboard is stale again until the");
Console.WriteLine("projection next runs. That window is the price of the split.");

writeModel.Execute(new PlaceOrder("ORD-4", "CUST-2", 200m));
Console.WriteLine($"  dashboard for CUST-2: {Describe(readModel.SummaryFor("CUST-2"))}  <- stale");

projection.Run();
Console.WriteLine($"  dashboard for CUST-2: {Describe(readModel.SummaryFor("CUST-2"))}");

static string Describe(OrderSummary? summary) =>
    summary is null
        ? "no summary yet"
        : $"{summary.Value.Orders} order(s), {summary.Value.Total:0.00} total";
