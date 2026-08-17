using Sidecar;

// An application with a metrics sidecar. The application contains no metrics
// code at all; the telemetry exists because something was deployed beside it.

Console.WriteLine("Sidecar - a helper that shares the application's lifecycle");
Console.WriteLine(new string('=', 62));
Console.WriteLine();

MetricsSidecar sidecar = new();
ApplicationHost host = new(sidecar);

Console.WriteLine("Before anything starts");
Console.WriteLine(new string('-', 62));
Console.WriteLine($"  application running: {host.Running}");
Console.WriteLine($"  sidecar status:      {sidecar.Status}");

Console.WriteLine();
Console.WriteLine("The application starts");
Console.WriteLine(new string('-', 62));

host.Start();
Console.WriteLine($"  application running: {host.Running}");
Console.WriteLine($"  sidecar status:      {sidecar.Status}");
Console.WriteLine("  Nobody started the sidecar. It came up because the application");
Console.WriteLine("  did, which is what makes it a sidecar rather than a service");
Console.WriteLine("  somebody has to remember to deploy.");

Console.WriteLine();
Console.WriteLine("Two requests");
Console.WriteLine(new string('-', 62));

Console.WriteLine($"  {host.Handle("/orders/1042")}");
Console.WriteLine($"  {host.Handle("/catalogue/sku-77")}");

Console.WriteLine();
Console.WriteLine($"  the application's own record: {string.Join(", ", host.Handled)}");
Console.WriteLine($"  the sidecar's observations:   {string.Join(", ", sidecar.Collected)}");
Console.WriteLine();
Console.WriteLine("  No metrics code in the application. The observations exist");
Console.WriteLine("  because the sidecar was there, not because the application");
Console.WriteLine("  cooperated.");

Console.WriteLine();
Console.WriteLine("The application stops");
Console.WriteLine(new string('-', 62));

host.Stop();
Console.WriteLine($"  application running: {host.Running}");
Console.WriteLine($"  sidecar status:      {sidecar.Status}");
Console.WriteLine("  It goes down too. A helper that outlives its application is a");
Console.WriteLine("  leak; one that survives a rescheduled pod is somebody's incident.");

Console.WriteLine();
Console.WriteLine("A separate deployment, whose sidecar cannot start");
Console.WriteLine(new string('-', 62));

MetricsSidecar broken = new(failsOnStart: true);
ApplicationHost stillServing = new(broken);
stillServing.Start();

Console.WriteLine($"  sidecar status:      {stillServing.SidecarStatus}");
Console.WriteLine($"  application running: {stillServing.Running}");
Console.WriteLine($"  a request:           {stillServing.Handle("/orders/1042")}");
Console.WriteLine($"  observations:        {(broken.Collected.Count == 0 ? "none" : string.Join(", ", broken.Collected))}");
Console.WriteLine();
Console.WriteLine("  Telemetry is lost and the application is serving. The reverse -");
Console.WriteLine("  losing the application because its telemetry failed - is the");
Console.WriteLine("  mistake that makes teams distrust sidecars.");
Console.WriteLine();
Console.WriteLine("  And the failure is visible rather than silent: isolated is not");
Console.WriteLine("  the same as invisible.");
