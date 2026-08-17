namespace Saga.Tests;

/// <summary>
/// What a saga guarantees: that the sequence **and its compensations are one
/// durable unit**, recorded as it goes, so a coordinator that dies mid-run can
/// be replaced by one that reads the log and neither repeats nor forgets a step.
///
/// The log is the pattern. Remove it and what remains is a loop with a
/// <c>catch</c>, which is why the recovery tests here assert against a fresh
/// coordinator holding nothing but the log.
/// </summary>
public class OrderSagaTests
{
    private static Func<string, bool> Records(List<string> calls, string? failAt = null) =>
        step =>
        {
            calls.Add(step);
            return step != failAt;
        };

    [Fact]
    public void Records_each_step_as_it_completes()
    {
        SagaLog log = new();
        List<string> performed = [];
        OrderSaga saga = new(log, Records(performed), _ => true);

        saga.Run();

        Assert.Equal(
            ["payment", "payment", "inventory", "inventory", "shipping", "shipping"],
            log.Entries.Select(entry => entry.Step));
        Assert.Equal(
            [SagaStatus.Started, SagaStatus.Completed],
            log.Entries.Take(2).Select(entry => entry.Status));
    }

    [Fact]
    public void Compensates_completed_steps_when_a_step_fails()
    {
        SagaLog log = new();
        List<string> compensated = [];
        OrderSaga saga = new(log, Records([], failAt: "shipping"), Records(compensated));

        saga.Run();

        // Last first, and only the steps that had completed.
        Assert.Equal(["inventory", "payment"], compensated);
    }

    [Fact]
    public void Records_a_compensation_that_reports_failure_as_failed()
    {
        SagaLog log = new();
        OrderSaga saga = new(
            log,
            Records([], failAt: "shipping"),
            Records([], failAt: "payment"));

        saga.Run();

        // The log tells the truth: inventory was countered, payment's counter
        // failed and the step still stands. Recording Compensated here would
        // lie to the operator and to any replacement coordinator.
        Assert.Contains(new SagaEntry("inventory", SagaStatus.Compensated), log.Entries);
        Assert.Contains(new SagaEntry("payment", SagaStatus.CompensationFailed), log.Entries);
        Assert.True(log.IsComplete("payment"));
    }

    [Fact]
    public void Recovers_from_the_log_alone_after_a_restart()
    {
        SagaLog log = new();
        List<string> before = [];

        // A coordinator that gets as far as inventory and is then lost. Nothing
        // survives it except what it wrote down.
        OrderSaga lost = new(log, Records(before, failAt: "shipping"), _ => true);
        lost.RunUpTo("inventory");

        List<string> after = [];
        OrderSaga replacement = new(log, Records(after), _ => true);
        SagaStatus status = replacement.Run();

        Assert.Equal(SagaStatus.Completed, status);
        Assert.Equal(["shipping"], after);
    }

    [Fact]
    public void Does_not_repeat_a_step_the_log_shows_complete()
    {
        SagaLog log = new();
        OrderSaga first = new(log, _ => true, _ => true);
        first.RunUpTo("payment");

        List<string> second = [];
        OrderSaga replacement = new(log, Records(second), _ => true);
        replacement.Run();

        // Charging the card twice is the failure this pattern exists to prevent.
        Assert.DoesNotContain("payment", second);
    }

    [Fact]
    public void Marks_the_saga_failed_once_compensation_finishes()
    {
        SagaLog log = new();
        OrderSaga saga = new(log, Records([], failAt: "shipping"), _ => true);

        SagaStatus status = saga.Run();

        Assert.Equal(SagaStatus.Failed, status);
        Assert.Equal(SagaStatus.Failed, log.Entries[^1].Status);
        Assert.Equal(
            2,
            log.Entries.Count(entry => entry.Status == SagaStatus.Compensated));
    }

    [Fact]
    public void Reports_an_empty_log_as_nothing_to_recover()
    {
        SagaLog log = new();

        Assert.Empty(log.Entries);
        Assert.False(log.IsComplete("payment"));
    }
}
