namespace PrivacyBrowser.App;

public sealed record PaymentTarget(string GatewayName, Uri PaymentUri);
