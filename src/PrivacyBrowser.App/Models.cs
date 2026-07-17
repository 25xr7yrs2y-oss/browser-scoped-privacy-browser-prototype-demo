using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrivacyBrowser.App;

public sealed record BackendSnapshot(
    bool NodeUp,
    ConnectionInfo Connection,
    IdentityDetails? Identity,
    TermsStatus? Terms,
    IReadOnlyList<BackendIssue> Issues,
    DateTimeOffset ObservedAt)
{
    public bool IsConnected => Connection.Status.Equals("CONNECTED", StringComparison.OrdinalIgnoreCase);

    public static BackendSnapshot Offline(string reason = "The Myst backend is not responding.") =>
        new(false, new ConnectionInfo { Status = "BACKEND_OFFLINE" }, null, null,
            [new BackendIssue("healthcheck", reason)], DateTimeOffset.Now);
}

public sealed record BackendIssue(string Area, string Message);

public sealed class TermsStatus
{
    [JsonPropertyName("agreed_consumer")]
    public bool AgreedConsumer { get; set; }

    [JsonPropertyName("agreed_version")]
    public string AgreedVersion { get; set; } = "";

    [JsonPropertyName("current_version")]
    public string CurrentVersion { get; set; } = "";

    [JsonIgnore]
    public bool IsCurrent => AgreedConsumer &&
        AgreedVersion.Equals(CurrentVersion, StringComparison.OrdinalIgnoreCase);
}

public sealed class ConnectionInfo
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "NOT_CONNECTED";

    [JsonPropertyName("consumer_id")]
    public string ConsumerId { get; set; } = "";

    [JsonPropertyName("proposal")]
    public ProviderProposal? Proposal { get; set; }

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = "";
}

public sealed class IdentityList
{
    [JsonPropertyName("identities")]
    public List<IdentityReference> Identities { get; set; } = [];
}

public sealed class IdentityReference
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}

public sealed class IdentityDetails
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("registration_status")]
    public string RegistrationStatus { get; set; } = "Unavailable";

    [JsonPropertyName("channel_address")]
    public string ChannelAddress { get; set; } = "";

    [JsonPropertyName("balance_tokens")]
    public TokenAmount BalanceTokens { get; set; } = new();

    [JsonIgnore]
    public bool IsRegistered => RegistrationStatus.Equals("Registered", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool RegistrationInProgress => RegistrationStatus.Equals("InProgress", StringComparison.OrdinalIgnoreCase) ||
        RegistrationStatus.Equals("In progress", StringComparison.OrdinalIgnoreCase);
}

public sealed class BalanceStatus
{
    [JsonPropertyName("balance_tokens")]
    public TokenAmount BalanceTokens { get; set; } = new();
}

public sealed class TokenAmount
{
    [JsonPropertyName("wei")]
    public string Wei { get; set; } = "0";

    [JsonPropertyName("ether")]
    public string Ether { get; set; } = "0";

    [JsonPropertyName("human")]
    public string Human { get; set; } = "0";

    [JsonIgnore]
    public decimal Value
    {
        get
        {
            var value = string.IsNullOrWhiteSpace(Ether) ? Human : Ether;
            return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? result
                : 0m;
        }
    }

    [JsonIgnore]
    public string Display => Value == 0m ? "0" : Value.ToString("0.####", CultureInfo.InvariantCulture);
}

public sealed class ProposalList
{
    [JsonPropertyName("proposals")]
    public List<ProviderProposal> Proposals { get; set; } = [];
}

public sealed class ProviderProposal
{
    [JsonPropertyName("provider_id")]
    public string ProviderId { get; set; } = "";

    [JsonPropertyName("service_type")]
    public string ServiceType { get; set; } = "wireguard";

    [JsonPropertyName("location")]
    public ProviderLocation Location { get; set; } = new();

    [JsonPropertyName("price")]
    public ProviderPrice Price { get; set; } = new();

    [JsonPropertyName("quality")]
    public ProviderQuality Quality { get; set; } = new();

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var place = string.Join(", ", new[] { Location.City, Location.Country }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var shortId = ProviderId.Length > 12 ? ProviderId[..12] + "…" : ProviderId;
            return string.IsNullOrWhiteSpace(place) ? shortId : $"{place}  ·  {shortId}";
        }
    }

    [JsonIgnore]
    public string PriceSummary
    {
        get
        {
            var currency = string.IsNullOrWhiteSpace(Price.Currency) ? "MYST" : Price.Currency;
            return $"{Price.PerGiBTokens.Display} {currency}/GiB + {Price.PerHourTokens.Display} {currency}/hour";
        }
    }
}

public sealed class ProviderLocation
{
    [JsonPropertyName("country")]
    public string Country { get; set; } = "";

    [JsonPropertyName("city")]
    public string City { get; set; } = "";

    [JsonPropertyName("ip_type")]
    public string IpType { get; set; } = "";

    [JsonPropertyName("isp")]
    public string Isp { get; set; } = "";
}

public sealed class ProviderPrice
{
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "MYST";

    [JsonPropertyName("per_hour_tokens")]
    public TokenAmount PerHourTokens { get; set; } = new();

    [JsonPropertyName("per_gib_tokens")]
    public TokenAmount PerGiBTokens { get; set; } = new();
}

public sealed class ProviderQuality
{
    [JsonPropertyName("quality")]
    public double Quality { get; set; }

    [JsonPropertyName("latency")]
    public double Latency { get; set; }

    [JsonPropertyName("bandwidth")]
    public double Bandwidth { get; set; }
}

public sealed class PaymentGateway
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("order_options")]
    public PaymentOrderOptions OrderOptions { get; set; } = new();

    [JsonPropertyName("currencies")]
    public List<string> Currencies { get; set; } = [];

    [JsonIgnore]
    public string DisplayName => Name.Replace('_', ' ');
}

public sealed class PaymentOrderOptions
{
    [JsonPropertyName("minimum")]
    public decimal Minimum { get; set; }

    [JsonPropertyName("suggested")]
    public List<decimal> Suggested { get; set; } = [];
}

public sealed class PaymentOrder
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("gateway_name")]
    public string GatewayName { get; set; } = "";

    [JsonPropertyName("receive_myst")]
    public string ReceiveMyst { get; set; } = "";

    [JsonPropertyName("pay_amount")]
    public string PayAmount { get; set; } = "";

    [JsonPropertyName("pay_currency")]
    public string PayCurrency { get; set; } = "";

    [JsonPropertyName("public_gateway_data")]
    public JsonElement PublicGatewayData { get; set; }

    public Uri? FindPaymentUri() => FindUri(PublicGatewayData);

    private static Uri? FindUri(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String &&
            Uri.TryCreate(element.GetString(), UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            return uri;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var result = FindUri(property.Value);
                if (result is not null) return result;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var result = FindUri(item);
                if (result is not null) return result;
            }
        }

        return null;
    }
}
