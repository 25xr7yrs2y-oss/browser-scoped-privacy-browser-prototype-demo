using System.Text.Json;

namespace PrivacyBrowser.App;

public sealed class CoinGatePaymentGatewayAdapter : IPaymentGatewayAdapter
{
    public const string CanonicalGatewayName = "coingate";
    private const string PaymentUrlField = "paymentUrl";

    public string GatewayName => CanonicalGatewayName;

    public PaymentTarget ParsePaymentTarget(JsonElement publicGatewayData)
    {
        if (publicGatewayData.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("The payment response did not contain valid gateway data.");
        }

        JsonElement paymentUrl = default;
        var matchingFields = 0;
        foreach (var property in publicGatewayData.EnumerateObject())
        {
            if (!property.Name.Equals(PaymentUrlField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matchingFields++;
            if (!property.NameEquals(PaymentUrlField))
            {
                throw new InvalidOperationException("The payment response contained an ambiguous payment URL field.");
            }
            paymentUrl = property.Value;
        }

        if (matchingFields != 1 || paymentUrl.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("The payment response did not contain one valid payment URL field.");
        }

        return new PaymentTarget(GatewayName, PaymentUriValidator.ParseAbsoluteHttps(paymentUrl.GetString()!));
    }
}
