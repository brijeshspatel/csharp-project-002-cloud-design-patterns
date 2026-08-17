namespace Cqrs.Tests;

/// <summary>
/// What CQRS guarantees: that commands and queries travel through **different
/// models** — one shaped for deciding, one shaped for asking — and that the
/// price of the split is a read model which is stale until the projection runs.
///
/// The staleness is asserted directly. It is not a defect to be hidden; it is
/// the cost of the pattern, and a reader should meet it in the tests.
/// </summary>
public class OrderModelsTests
{
    [Fact]
    public void Accepts_a_command_through_the_write_model()
    {
        OrderWriteModel writeModel = new();

        writeModel.Execute(new PlaceOrder("ORD-1", "CUST-1", 120m));

        Assert.Equal(1, writeModel.Count);
    }

    [Fact]
    public void Leaves_the_read_model_stale_until_the_projection_runs()
    {
        OrderWriteModel writeModel = new();
        OrderReadModel readModel = new();

        writeModel.Execute(new PlaceOrder("ORD-1", "CUST-1", 120m));

        // The command has landed and the dashboard does not know. This window
        // is the pattern's cost, stated as a test rather than a caveat.
        Assert.Null(readModel.SummaryFor("CUST-1"));
    }

    [Fact]
    public void Updates_the_read_model_when_the_projection_runs()
    {
        OrderWriteModel writeModel = new();
        OrderReadModel readModel = new();
        OrderProjection projection = new(writeModel, readModel);

        writeModel.Execute(new PlaceOrder("ORD-1", "CUST-1", 120m));
        projection.Run();

        OrderSummary? summary = readModel.SummaryFor("CUST-1");

        Assert.Equal(1, summary?.Orders);
        Assert.Equal(120m, summary?.Total);
    }

    [Fact]
    public void Serves_a_denormalised_shape_the_write_model_does_not_hold()
    {
        OrderWriteModel writeModel = new();
        OrderReadModel readModel = new();
        OrderProjection projection = new(writeModel, readModel);

        writeModel.Execute(new PlaceOrder("ORD-1", "CUST-1", 120m));
        writeModel.Execute(new PlaceOrder("ORD-2", "CUST-1", 80m));
        writeModel.Execute(new PlaceOrder("ORD-3", "CUST-2", 50m));
        projection.Run();

        // Per-customer totals exist nowhere in the write model; the projection
        // computed them into the shape the dashboard asks in.
        OrderSummary? summary = readModel.SummaryFor("CUST-1");

        Assert.Equal(2, summary?.Orders);
        Assert.Equal(200m, summary?.Total);
    }

    [Fact]
    public void Rejects_a_query_sent_to_the_write_model()
    {
        OrderWriteModel writeModel = new();

        writeModel.Execute(new PlaceOrder("ORD-1", "CUST-1", 120m));

        // The boundary as code: the write model refuses dashboard questions
        // rather than growing the read paths that erode the split.
        Assert.Throws<NotSupportedException>(() => writeModel.SummaryFor("CUST-1"));
    }
}
