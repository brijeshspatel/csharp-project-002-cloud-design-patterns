using PriorityQueue;

// A support desk. Five free-tier tickets arrive first and wait; a paid-tier
// ticket arrives last and is served first. Then the starvation risk is shown
// directly, because it is the thing this pattern is most often deployed
// without thinking about.

Console.WriteLine("Priority Queue - importance beats arrival, arrival decides between equals");
Console.WriteLine(new string('=', 72));
Console.WriteLine();

PriorityBacklog backlog = new();

Console.WriteLine("Arrivals");
Console.WriteLine(new string('-', 72));
for (int i = 1; i <= 5; i++)
{
    backlog.Enqueue(new SupportTicket($"TCK-{i:000}", "free-tier"), Priority.Low);
    Console.WriteLine($"  TCK-{i:000}  free-tier   Low");
}

backlog.Enqueue(new SupportTicket("TCK-006", "contoso"), Priority.Normal);
Console.WriteLine("  TCK-006  contoso     Normal");

backlog.Enqueue(new SupportTicket("TCK-007", "fabrikam"), Priority.High);
Console.WriteLine("  TCK-007  fabrikam    High     <- arrived last");

Console.WriteLine();
Console.WriteLine($"Waiting: {backlog.CountAt(Priority.High)} high, " +
                  $"{backlog.CountAt(Priority.Normal)} normal, " +
                  $"{backlog.CountAt(Priority.Low)} low");

Console.WriteLine();
Console.WriteLine("Served");
Console.WriteLine(new string('-', 72));

int position = 0;
while (backlog.TryDequeue(out SupportTicket ticket))
{
    position++;
    Console.WriteLine($"  {position}. {ticket.Reference}  {ticket.Customer}");
}

Console.WriteLine();
Console.WriteLine("The starvation risk, shown rather than described");
Console.WriteLine(new string('-', 72));

PriorityBacklog starving = new();
starving.Enqueue(new SupportTicket("TCK-100", "free-tier"), Priority.Low);

// A steady trickle of urgent work arrives, and each round serves one ticket.
for (int round = 1; round <= 5; round++)
{
    starving.Enqueue(new SupportTicket($"TCK-2{round:00}", "paid-tier"), Priority.High);
    starving.TryDequeue(out SupportTicket served);
    Console.WriteLine($"  round {round}: served {served.Reference}, " +
                      $"low-priority still waiting: {starving.CountAt(Priority.Low)}");
}

Console.WriteLine();
Console.WriteLine("TCK-100 arrived first and has still not been served. Strict priority");
Console.WriteLine("has no answer to this; ageing does. See this pattern's supporting document.");
