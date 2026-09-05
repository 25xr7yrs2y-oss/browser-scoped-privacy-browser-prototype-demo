using System.Text.Json;
using PrivacyBrowser.App;

var tests = new (string Name, Action Run)[]
{
    ("registered adapter matching is exact and fail-closed", RegisteredAdapterMatchingIsExactAndFailClosed),
    ("HTTPS without an explicit port", () => Accept("https://payments.example/order/1")),
    ("HTTPS with explicit port 443", () => Accept("https://payments.example:443/order/1")),
    ("mixed-case HTTPS scheme", () => Accept("HtTpS://payments.example/order/1")),
    ("HTTP", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"http://payments.example/order/1\"}")),
    ("mixed-case HTTP", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"HtTp://payments.example/order/1\"}")),
    ("username user-info", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"https://user@payments.example/order/1\"}")),
    ("username and password user-info", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"https://user:password@payments.example/order/1\"}")),
    ("percent-encoded user-info", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"https://user%40name@payments.example/order/1\"}")),
    ("non-default port", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"https://payments.example:444/order/1\"}")),
    ("explicit empty port", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"https://payments.example:/order/1\"}")),
    ("relative URL", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"/order/1\"}")),
    ("malformed URL", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"https://[invalid/order/1\"}")),
    ("unknown gateway", () => Reject("stripe", "stripe", "{\"paymentUrl\":\"https://payments.example/order/1\"}")),
    ("response gateway mismatch", () => Reject("coingate", "stripe", "{\"paymentUrl\":\"https://payments.example/order/1\"}")),
    ("missing field", () => Reject("coingate", "coingate", "{}")),
    ("null field", () => Reject("coingate", "coingate", "{\"paymentUrl\":null}")),
    ("numeric field", () => Reject("coingate", "coingate", "{\"paymentUrl\":42}")),
    ("object field", () => Reject("coingate", "coingate", "{\"paymentUrl\":{}}")),
    ("array field", () => Reject("coingate", "coingate", "{\"paymentUrl\":[]}")),
    ("wrong root type", () => Reject("coingate", "coingate", "[]")),
    ("incorrectly nested field", () => Reject("coingate", "coingate", "{\"wrapper\":{\"paymentUrl\":\"https://evil.example/\"}}")),
    ("unrelated URL is not selected", UnrelatedUrlIsNotSelected),
    ("unrelated-only URL is rejected", () => Reject("coingate", "coingate", "{\"helpUrl\":\"https://evil.example/\"}")),
    ("duplicate exact field", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"https://payments.example/one\",\"paymentUrl\":\"https://payments.example/two\"}")),
    ("case-shadowed field", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"https://payments.example/one\",\"PaymentUrl\":\"https://evil.example/two\"}")),
    ("fragment", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"https://payments.example/order/1#checkout\"}")),
    ("empty fragment", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"https://payments.example/order/1#\"}")),
    ("leading whitespace", () => Reject("coingate", "coingate", "{\"paymentUrl\":\" https://payments.example/order/1\"}")),
    ("embedded control character", () => Reject("coingate", "coingate", "{\"paymentUrl\":\"https://payments.example/order/\\u0001\"}")),
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS: {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL: {test.Name}: {exception.Message}");
    }
}

if (failures > 0)
{
    Console.Error.WriteLine($"{failures} payment gateway adapter test(s) failed.");
    return 1;
}

Console.WriteLine($"PASS: all {tests.Length} payment gateway adapter tests passed.");
return 0;

static void Accept(string value)
{
    var data = ParseData($"{{\"paymentUrl\":{JsonSerializer.Serialize(value)}}}");
    var target = PaymentGatewayRegistry.ParsePaymentTarget("coingate", "coingate", data);
    var uri = target.PaymentUri;
    if (!target.GatewayName.Equals("coingate", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Adapter returned the wrong canonical gateway: {target.GatewayName}");
    }
    if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
        !uri.IsAbsoluteUri ||
        !uri.IsDefaultPort ||
        !string.IsNullOrEmpty(uri.UserInfo))
    {
        throw new InvalidOperationException($"Accepted URI did not preserve the required invariants: {uri}");
    }
}

static void Reject(string expectedGateway, string responseGateway, string json)
{
    var data = ParseData(json);
    try
    {
        var result = PaymentGatewayRegistry.ParsePaymentTarget(expectedGateway, responseGateway, data);
        throw new InvalidOperationException($"Registry unexpectedly accepted {result.PaymentUri}.");
    }
    catch (InvalidOperationException exception) when (!exception.Message.StartsWith("Registry unexpectedly accepted", StringComparison.Ordinal))
    {
    }
}

static void RegisteredAdapterMatchingIsExactAndFailClosed()
{
    if (!PaymentGatewayRegistry.SupportsGateway("coingate"))
    {
        throw new InvalidOperationException("The verified CoinGate adapter was not registered.");
    }
    foreach (var unsupported in new string?[] { null, "", "CoinGate", "stripe", "paypal" })
    {
        if (PaymentGatewayRegistry.SupportsGateway(unsupported))
        {
            throw new InvalidOperationException($"An unregistered gateway was accepted: {unsupported}");
        }
    }
}

static void UnrelatedUrlIsNotSelected()
{
    var data = ParseData("{\"helpUrl\":\"https://evil.example/\",\"paymentUrl\":\"https://payments.example/order/1\",\"nested\":{\"url\":\"https://also-evil.example/\"}}");
    var uri = PaymentGatewayRegistry.ParsePaymentTarget("coingate", "coingate", data).PaymentUri;
    if (!uri.Host.Equals("payments.example", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Parser selected the wrong host: {uri.Host}");
    }
}

static JsonElement ParseData(string json)
{
    using var document = JsonDocument.Parse(json);
    return document.RootElement.Clone();
}
