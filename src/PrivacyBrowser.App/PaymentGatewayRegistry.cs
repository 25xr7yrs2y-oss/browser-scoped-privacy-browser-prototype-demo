using System.Text.Json;

namespace PrivacyBrowser.App;

public static class PaymentGatewayRegistry
{
    private static readonly IReadOnlyDictionary<string, IPaymentGatewayAdapter> Adapters =
        new Dictionary<string, IPaymentGatewayAdapter>(StringComparer.Ordinal)
        {
            [CoinGatePaymentGatewayAdapter.CanonicalGatewayName] = new CoinGatePaymentGatewayAdapter(),
        };

    public static bool SupportsGateway(string? gatewayName) =>
        gatewayName is not null && Adapters.ContainsKey(gatewayName);

    public static PaymentTarget ParsePaymentTarget(
        string expectedGatewayName,
        string responseGatewayName,
        JsonElement publicGatewayData)
    {
        if (!Adapters.TryGetValue(expectedGatewayName, out var adapter))
        {
            throw new InvalidOperationException("The selected payment gateway is not supported.");
        }
        if (!string.Equals(responseGatewayName, adapter.GatewayName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The payment response did not match the selected gateway.");
        }

        var target = adapter.ParsePaymentTarget(publicGatewayData);
        if (!string.Equals(target.GatewayName, adapter.GatewayName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The payment gateway adapter returned a mismatched target.");
        }

        return target;
    }
}
