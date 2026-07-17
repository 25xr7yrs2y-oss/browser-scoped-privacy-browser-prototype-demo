using System.Net;
using System.Text.Json;

namespace PrivacyBrowser.App;

public sealed class BackendApiException : InvalidOperationException
{
    public BackendApiException(string userMessage, string diagnosticMessage, string? code, HttpStatusCode? statusCode)
        : base(userMessage)
    {
        DiagnosticMessage = diagnosticMessage;
        Code = code;
        StatusCode = statusCode;
    }

    public string DiagnosticMessage { get; }
    public string? Code { get; }
    public HttpStatusCode? StatusCode { get; }
}

public static class BackendErrorTranslator
{
    public static BackendApiException FromResponse(HttpStatusCode statusCode, string? reason, string detail)
    {
        var code = FindJsonString(detail, "code");
        var backendMessage = FindJsonString(detail, "message") ?? FindJsonString(detail, "detail") ?? detail;
        var diagnostic = $"Backend request failed ({(int)statusCode} {reason})" +
            (string.IsNullOrWhiteSpace(detail) ? "." : $": {Limit(detail, 800)}");
        return new BackendApiException(ToUserMessage(code, backendMessage), diagnostic, code, statusCode);
    }

    public static string ToUserMessage(Exception exception)
    {
        if (exception is BackendApiException backend) return backend.Message;
        if (exception is TimeoutException || exception is TaskCanceledException)
        {
            return "The backend operation timed out. Check your internet connection and try again.";
        }
        if (exception is HttpRequestException)
        {
            return "The Myst backend is temporarily unavailable. Wait a moment and try again.";
        }
        return string.IsNullOrWhiteSpace(exception.Message)
            ? "The operation could not be completed."
            : exception.Message;
    }

    public static string? ToActivityMessage(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage)) return null;
        if (rawMessage.Contains("status=[Unregistered]", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("err_id_not_registered", StringComparison.OrdinalIgnoreCase))
        {
            return "Your Mysterium identity is not registered. Register it before connecting.";
        }
        if (rawMessage.Contains("timeout exceeded", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("request canceled", StringComparison.OrdinalIgnoreCase))
        {
            return "A Mysterium network request timed out. Check your connection, then retry.";
        }
        if (rawMessage.Contains("Backend exited", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("Started Myst backend", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("control endpoint is ready", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("Using an already-running backend", StringComparison.OrdinalIgnoreCase))
        {
            return rawMessage;
        }

        // Routine daemon output is intentionally kept out of the user-facing activity feed.
        return null;
    }

    private static string ToUserMessage(string? code, string backendMessage)
    {
        if (string.Equals(code, "err_id_not_registered", StringComparison.OrdinalIgnoreCase) ||
            backendMessage.Contains("not registered", StringComparison.OrdinalIgnoreCase) ||
            backendMessage.Contains("status=[Unregistered]", StringComparison.OrdinalIgnoreCase))
        {
            return "Your Mysterium identity is not registered. Register it before connecting.";
        }
        if (string.Equals(code, "err_id_registration_in_progress", StringComparison.OrdinalIgnoreCase))
        {
            return "Identity registration is already in progress. Refresh its status in a moment.";
        }
        if (string.Equals(code, "err_connection_already_exists", StringComparison.OrdinalIgnoreCase))
        {
            return "A provider connection already exists. Disconnect it before starting another.";
        }
        if (code?.StartsWith("err_payment", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Mysterium could not create or retrieve the payment. Review the top-up details and try again.";
        }
        if (backendMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            backendMessage.Contains("request canceled", StringComparison.OrdinalIgnoreCase))
        {
            return "Mysterium could not reach its network service in time. Check your internet connection and try again.";
        }
        if (string.Equals(code, "err_connect", StringComparison.OrdinalIgnoreCase))
        {
            return "The selected provider could not be reached. Refresh providers and try another one.";
        }
        if (string.Equals(code, "err_id_registration_status_check", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, "err_id_blockchain_registration_check", StringComparison.OrdinalIgnoreCase))
        {
            return "Mysterium could not verify identity registration. Check your internet connection and refresh status.";
        }

        var clean = backendMessage.Trim();
        return string.IsNullOrWhiteSpace(clean) || clean.StartsWith('{')
            ? "The backend rejected the request. Refresh status and try again."
            : Limit(clean, 300);
    }

    private static string? FindJsonString(string json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return FindJsonString(document.RootElement, propertyName);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FindJsonString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }
                var nested = FindJsonString(property.Value, propertyName);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindJsonString(item, propertyName);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        return null;
    }

    private static string Limit(string value, int length) => value.Length <= length ? value : value[..length] + "…";
}
