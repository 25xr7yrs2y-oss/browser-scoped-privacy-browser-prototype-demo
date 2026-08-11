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
        if (HasExplicitEmptyPort(value) || !uri.IsDefaultPort)
        {
            throw new InvalidOperationException("The payment URL must use the default HTTPS port.");
        }

        return uri;
    }

    // System.Uri normalizes an explicit empty port ("https://host:") to the default port,
    // so inspect the raw authority before relying on IsDefaultPort.
    private static bool HasExplicitEmptyPort(string value)
    {
        var authorityStart = value.IndexOf("://", StringComparison.Ordinal);
        if (authorityStart < 0)
        {
            return true;
        }

        authorityStart += 3;
        var authorityEnd = value.Length;
        foreach (var separator in new[] { '/', '?', '#' })
        {
            var index = value.IndexOf(separator, authorityStart);
            if (index >= 0 && index < authorityEnd)
            {
                authorityEnd = index;
            }
        }

        return authorityEnd > authorityStart && value[authorityEnd - 1] == ':';
    }
}
