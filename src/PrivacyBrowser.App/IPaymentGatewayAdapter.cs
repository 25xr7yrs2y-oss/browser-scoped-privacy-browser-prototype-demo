using System.Text.Json;

namespace PrivacyBrowser.App;

public interface IPaymentGatewayAdapter
{
    string GatewayName { get; }

    PaymentTarget ParsePaymentTarget(JsonElement publicGatewayData);
}
