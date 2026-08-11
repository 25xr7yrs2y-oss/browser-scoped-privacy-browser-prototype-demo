using System.Text.Json;

namespace PrivacyBrowser.App;

public static class PaymentTargetParser
{
    private const string CoinGateGateway = "coingate";
    private const string CoinGatePaymentUrlField = "paymentUrl";

    public static bool SupportsGateway(string? gatewayName) =>
        string.Equals(gatewayName, CoinGateGateway, StringComparison.Ordinal);

    public static Uri GetPaymentUri(
        string expectedGatewayName,
        string responseGatewayName,
        JsonElement publicGatewayData)
    {
        if (!SupportsGateway(expectedGatewayName))
        {
            throw new InvalidOperationException("The selected payment gateway is not supported.");
        }
        if (!string.Equals(responseGatewayName, expectedGatewayName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The payment response did not match the selected gateway.");
        }
        if (publicGatewayData.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("The payment response did not contain valid gateway data.");
        }

        JsonElement paymentUrl = default;
        var matchingFields = 0;
        foreach (var property in publicGatewayData.EnumerateObject())
        {
            if (!property.Name.Equals(CoinGatePaymentUrlField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matchingFields++;
            if (!property.NameEquals(CoinGatePaymentUrlField))
            {
                throw new InvalidOperationException("The payment response contained an ambiguous payment URL field.");
            }
            paymentUrl = property.Value;
        }

        if (matchingFields != 1 || paymentUrl.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("The payment response did not contain one valid payment URL field.");
        }

        var value = paymentUrl.GetString();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException("The payment response contained an empty payment URL.");
        }
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                throw new InvalidOperationException("The payment URL contained whitespace or control characters.");
            }
        }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(uri.Host))
        {
            throw new InvalidOperationException("The payment URL must be an absolute HTTPS URL.");
        }
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException("The payment URL must not contain user information.");
        }
        if (!uri.IsDefaultPort)
        {
            throw new InvalidOperationException("The payment URL must use the default HTTPS port.");
        }

        return uri;
    }
}
