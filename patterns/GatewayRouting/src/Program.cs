using GatewayRouting;

// One public endpoint, four services behind it - and a client that knows about
// none of them.

Console.WriteLine("Gateway Routing - one endpoint, several services");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

BackendService ordersV1 = new("orders-v1");
BackendService ordersV2 = new("orders-v2");
BackendService catalogue = new("catalogue");
BackendService accounts = new("accounts");

RouteTable routes = new();
routes.Add("/v1/orders", ordersV1);
routes.Add("/v2/orders", ordersV2);
routes.Add("/catalogue", catalogue);
routes.Add("/accounts", accounts);

RequestGateway gateway = new(routes);

Console.WriteLine($"  routes: {string.Join(", ", gateway.Routes)}");
Console.WriteLine();

Console.WriteLine("The client sends everything to the same endpoint");
Console.WriteLine(new string('-', 60));

string[] paths =
[
    "/v1/orders/1042",
    "/v2/orders/1042",
    "/catalogue/sku-77",
    "/accounts/me",
];

foreach (string path in paths)
{
    ServiceResponse response = gateway.Send(new ClientRequest(path, string.Empty));
    Console.WriteLine($"  {path,-22} -> {response.ServedBy}");
}

Console.WriteLine();
Console.WriteLine("Two versions of one resource, behind one endpoint");
Console.WriteLine(new string('-', 60));
Console.WriteLine("  /v1/orders and /v2/orders are different services. The client");
Console.WriteLine("  changed a URL; nothing was redeployed to make that work.");

Console.WriteLine();
Console.WriteLine("A path nothing owns is refused");
Console.WriteLine(new string('-', 60));

int before = ordersV1.Handled + ordersV2.Handled + catalogue.Handled + accounts.Handled;
ServiceResponse missing = gateway.Send(new ClientRequest("/invoices/9", string.Empty));
int after = ordersV1.Handled + ordersV2.Handled + catalogue.Handled + accounts.Handled;

Console.WriteLine($"  /invoices/9 -> {missing.Body}");
Console.WriteLine($"  requests that reached a service: {after - before}");
Console.WriteLine("  Falling through to something plausible would turn a typo into");
Console.WriteLine("  a real request against the wrong service.");

Console.WriteLine();
Console.WriteLine("A new service appears, and no client is redeployed");
Console.WriteLine(new string('-', 60));

routes.Add("/offers", new BackendService("offers"));
Console.WriteLine($"  /offers -> {gateway.Send(new ClientRequest("/offers", string.Empty)).ServedBy}");
Console.WriteLine($"  routes: {string.Join(", ", gateway.Routes)}");

Console.WriteLine();
Console.WriteLine("The routing table is the only place that knows the topology.");
Console.WriteLine("Everything a gateway is for follows from that being true.");
