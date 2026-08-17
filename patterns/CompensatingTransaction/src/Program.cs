using CompensatingTransaction;

// A trip booked in four steps, where the last one fails. The interesting run is
// this one: the undo, not the success.

Console.WriteLine("Compensating Transaction - undoing what cannot be rolled back");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

List<string> world = [];

BookingStep email = BookingStep.WithoutCompensation(
    "confirmation-email",
    () => { world.Add("email sent to the traveller"); return true; });

BookingStep flight = new(
    "flight",
    () => { world.Add("flight AB123 held"); return true; },
    () => { world.Remove("flight AB123 held"); return true; });

BookingStep hotel = new(
    "hotel",
    () => { world.Add("hotel room 402 held"); return true; },
    // The hotel's cancellation endpoint is down. A compensation is a request to
    // a system that can refuse, which is the case a rollback never has.
    () => false);

BookingStep car = new(
    "car",
    // No cars left. The step changed nothing, which is why nothing counters it.
    () => false,
    () => { world.Remove("car reserved"); return true; });

TripBooking booking = new([email, flight, hotel, car]);
BookingOutcome outcome = booking.Book();

Console.WriteLine("What happened, in order");
Console.WriteLine(new string('-', 62));
foreach (StepRecord record in booking.History)
{
    string verb = record.WasCompensation ? "undo" : "do  ";
    string result = record.Succeeded ? "ok" : "FAILED";
    Console.WriteLine($"  {verb}  {record.Step,-20} {result}");
}

Console.WriteLine();
Console.WriteLine($"  succeeded: {outcome.Succeeded}");
Console.WriteLine($"  failed at: {outcome.FailedStep}");

Console.WriteLine();
Console.WriteLine("Compensation ran last-first: hotel before flight. Undoing in the");
Console.WriteLine("order the steps ran would release the flight while the hotel");
Console.WriteLine("booking still depended on the trip existing.");

Console.WriteLine();
Console.WriteLine("What could not be undone");
Console.WriteLine(new string('-', 62));
foreach (string name in outcome.NotUndone)
{
    Console.WriteLine($"  {name}");
}

Console.WriteLine();
Console.WriteLine("The state of the world now");
Console.WriteLine(new string('-', 62));
foreach (string fact in world)
{
    Console.WriteLine($"  {fact}");
}

Console.WriteLine();
Console.WriteLine("Neither of those is a defect in the model. A compensation is a new");
Console.WriteLine("action that can fail, and some steps - an email already sent - have");
Console.WriteLine("no counter at all. A workflow that reported success here would be");
Console.WriteLine("lying about the world it left behind.");
