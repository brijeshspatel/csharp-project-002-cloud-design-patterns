namespace CompensatingTransaction.Tests;

/// <summary>
/// What a compensating transaction guarantees: that work already committed is
/// undone by **new actions running in reverse order**, and that the model is
/// honest about the two cases which make this hard — a compensation that fails,
/// and a step that cannot be compensated at all.
///
/// The order is asserted rather than the end state, because a compensation set
/// that ran in any order leaves the same end state here and would not in any
/// real system.
/// </summary>
public class TripBookingTests
{
    private static BookingStep Succeeds(string name, List<string> trace) =>
        new(name,
            () => { trace.Add($"do:{name}"); return true; },
            () => { trace.Add($"undo:{name}"); return true; });

    private static BookingStep Fails(string name, List<string> trace) =>
        new(name,
            () => { trace.Add($"do:{name}"); return false; },
            () => { trace.Add($"undo:{name}"); return true; });

    [Fact]
    public void Completes_every_step_when_none_fail()
    {
        List<string> trace = [];
        TripBooking booking = new([
            Succeeds("flight", trace),
            Succeeds("hotel", trace),
            Succeeds("car", trace)]);

        BookingOutcome outcome = booking.Book();

        Assert.True(outcome.Succeeded);
        Assert.Equal(["do:flight", "do:hotel", "do:car"], trace);
    }

    [Fact]
    public void Compensates_completed_steps_in_reverse_order()
    {
        List<string> trace = [];
        TripBooking booking = new([
            Succeeds("flight", trace),
            Succeeds("hotel", trace),
            Fails("car", trace)]);

        BookingOutcome outcome = booking.Book();

        Assert.False(outcome.Succeeded);

        // Hotel before flight. Undoing in the order the steps ran would release
        // the flight while the hotel still depends on the trip existing, which
        // is the ordinary reason reverse order matters.
        Assert.Equal(
            ["do:flight", "do:hotel", "do:car", "undo:hotel", "undo:flight"],
            trace);
    }

    [Fact]
    public void Does_not_compensate_the_step_that_failed()
    {
        List<string> trace = [];
        TripBooking booking = new([
            Succeeds("flight", trace),
            Fails("car", trace)]);

        booking.Book();

        // The car was never booked, so there is nothing to counter. Undoing it
        // would be a second, unrelated change to the world.
        Assert.DoesNotContain("undo:car", trace);
    }

    [Fact]
    public void Reports_a_compensation_that_itself_failed()
    {
        BookingStep flight = new("flight", () => true, () => false);
        TripBooking booking = new([flight, new BookingStep("car", () => false, () => true)]);

        BookingOutcome outcome = booking.Book();

        // A compensation is a new action against a system that may itself be
        // down. Swallowing that leaves the caller believing the world was
        // restored when it was not.
        Assert.Contains("flight", outcome.NotUndone);
    }

    [Fact]
    public void Reports_a_step_that_cannot_be_compensated()
    {
        BookingStep email = BookingStep.WithoutCompensation("confirmation-email", () => true);
        TripBooking booking = new([email, new BookingStep("car", () => false, () => true)]);

        BookingOutcome outcome = booking.Book();

        Assert.Contains("confirmation-email", outcome.NotUndone);
    }

    [Fact]
    public void Records_every_step_and_compensation_in_order()
    {
        List<string> trace = [];
        TripBooking booking = new([
            Succeeds("flight", trace),
            Succeeds("hotel", trace),
            Fails("car", trace)]);

        booking.Book();

        Assert.Equal(
            ["flight", "hotel", "car", "hotel", "flight"],
            booking.History.Select(record => record.Step));
        Assert.Equal(
            [false, false, false, true, true],
            booking.History.Select(record => record.WasCompensation));
    }
}
