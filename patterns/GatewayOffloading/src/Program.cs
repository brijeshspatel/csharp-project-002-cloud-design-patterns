using GatewayOffloading;

// Three services behind one proxy. The cross-cutting work happens once at the
// edge, and the saving is counted in implementations that do not exist.

Console.WriteLine("Gateway Offloading - work every service stopped doing");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

ApplicationService orders = new("orders");
ApplicationService catalogue = new("catalogue");
ApplicationService accounts = new("accounts");

OffloadingProxy proxy = new();
proxy.Register("/orders", orders);
proxy.Register("/catalogue", catalogue);
proxy.Register("/accounts", accounts);

Console.WriteLine("A request with a valid token");
Console.WriteLine(new string('-', 60));

EdgeResult ok = proxy.Send(new EdgeRequest("/orders", "token-valid", "list"));
Console.WriteLine($"  allowed:    {ok.Allowed}");
Console.WriteLine($"  body:       {ok.Body}");
Console.WriteLine($"  compressed: {ok.Compressed}");

Console.WriteLine();
Console.WriteLine("The same request with an expired token");
Console.WriteLine(new string('-', 60));

int before = orders.Handled;
EdgeResult refused = proxy.Send(new EdgeRequest("/orders", "token-expired", "list"));
Console.WriteLine($"  allowed: {refused.Allowed}");
Console.WriteLine($"  reason:  {refused.Reason}");
Console.WriteLine($"  requests that reached the service: {orders.Handled - before}");

Console.WriteLine();
Console.WriteLine("What the service actually contains");
Console.WriteLine(new string('-', 60));

string plain = orders.Handle(new EdgeRequest("/orders", string.Empty, "list"));
Console.WriteLine($"  called directly, with no token at all: {plain}");
Console.WriteLine();
Console.WriteLine("  It answered. Validating tokens is not its job and it does not");
Console.WriteLine("  know how. A service that refused here would have kept the code");
Console.WriteLine("  the proxy is supposed to have taken - and the offloading would");
Console.WriteLine("  be a hop rather than a saving.");
Console.WriteLine();
Console.WriteLine($"  note also: no 'gzip' in that response. Compression happened at");
Console.WriteLine($"  the edge, which is the only place that knows what the client accepts.");

Console.WriteLine();
Console.WriteLine("The saving, counted");
Console.WriteLine(new string('-', 60));
Console.WriteLine($"  concerns handled at the edge:  {string.Join(", ", proxy.Concerns)}");
Console.WriteLine($"  services behind the proxy:     {proxy.ServicesBehind}");
Console.WriteLine($"  implementations without a proxy: {proxy.ConcernsAtEdge * proxy.ServicesBehind}");
Console.WriteLine($"  implementations with one:        {proxy.ConcernsAtEdge}");
Console.WriteLine($"  avoided:                         {proxy.ImplementationsAvoided}");

Console.WriteLine();
Console.WriteLine("The saving is not per request. It is code that does not exist,");
Console.WriteLine("and therefore cannot drift between services or be patched in");
Console.WriteLine("eight places out of nine.");
