using System.Text.Json.Serialization;

namespace PrivacyBrowser.App;

public sealed record BackendSnapshot(bool NodeUp, string ConnectionStatus, IdentityDetails? Identity, TermsStatus? Terms)
{
    public bool IsConnected => ConnectionStatus.Equals("CONNECTED", StringComparison.OrdinalIgnoreCase);
}

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

public sealed class HealthCheck
{
    [JsonPropertyName("process")]
    public int ProcessId { get; set; }
}

public sealed class ConnectionInfo
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "NOT_CONNECTED";
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
    public string RegistrationStatus { get; set; } = "Unknown";
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

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var place = string.Join(", ", new[] { Location.City, Location.Country }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var shortId = ProviderId.Length > 14 ? ProviderId[..14] + "…" : ProviderId;
            return string.IsNullOrWhiteSpace(place) ? shortId : $"{place}  ·  {shortId}";
        }
    }
}

public sealed class ProviderLocation
{
    [JsonPropertyName("country")]
    public string Country { get; set; } = "";

    [JsonPropertyName("city")]
    public string City { get; set; } = "";
}
