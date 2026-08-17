using AntiCorruptionLayer;

// A modern ordering system reading and writing a mainframe that will not
// change. Everything the mainframe knows stops at the translator.

Console.WriteLine("Anti-Corruption Layer - where the legacy model stops");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

LegacyMainframe mainframe = new();
mainframe.Load(new LegacyOrderRecord("0000001042", "C0007", 54900, "A", "20260818"));
mainframe.Load(new LegacyOrderRecord("0000001043", "C0007", 12550, "B", "20260818"));

OrderTranslator translator = new(mainframe);

Console.WriteLine("What the mainframe holds");
Console.WriteLine(new string('-', 60));
foreach (string number in new[] { "0000001042", "0000001043" })
{
    LegacyOrderRecord? record = mainframe.Fetch(number);
    Console.WriteLine($"  ORDNO={record?.OrderNumber} CUSTNO={record?.CustomerNumber} " +
                      $"AMTPENCE={record?.AmountInPence} STATCD={record?.StatusCode} DTEORD={record?.OrderDate}");
}

Console.WriteLine();
Console.WriteLine("What the modern system sees");
Console.WriteLine(new string('-', 60));
foreach (string reference in new[] { "ORD-1042", "ORD-1043" })
{
    ModernOrder? order = translator.Read(reference);
    Console.WriteLine($"  {order?.Reference} for {order?.CustomerId}, " +
                      $"{order?.Amount:0.00} placed {order?.PlacedOn:d MMMM yyyy}, {order?.Status}");
}

Console.WriteLine();
Console.WriteLine("  No padded numbers, no pence, no single-letter codes, no yyyyMMdd");
Console.WriteLine("  strings. None of the mainframe's model crossed the boundary.");

Console.WriteLine();
Console.WriteLine("Where the two models disagree");
Console.WriteLine(new string('-', 60));
foreach (string concession in translator.Concessions)
{
    Console.WriteLine($"  {concession}");
}

Console.WriteLine();
Console.WriteLine("  That list is the honest artefact of the pattern. A translation");
Console.WriteLine("  with no concessions is either trivial or lying.");

Console.WriteLine();
Console.WriteLine("Writing back, in the mainframe's terms");
Console.WriteLine(new string('-', 60));

translator.Write(new ModernOrder("ORD-1042", "CUST-7", 549.00m, "shipped", new DateOnly(2026, 8, 18)));

LegacyOrderRecord written = mainframe.Posted[0];
Console.WriteLine($"  ORDNO={written.OrderNumber} CUSTNO={written.CustomerNumber} " +
                  $"AMTPENCE={written.AmountInPence} STATCD={written.StatusCode} DTEORD={written.OrderDate}");
Console.WriteLine();
Console.WriteLine("  Both directions, deliberately. A layer that only translates");
Console.WriteLine("  inward leaves every write path reaching around it, and within a");
Console.WriteLine("  year the modern code is padding order numbers itself.");
