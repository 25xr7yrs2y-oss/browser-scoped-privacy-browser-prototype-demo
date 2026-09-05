namespace PrivacyBrowser.App;

public static class PaymentUriValidator
{
    public static Uri ParseAbsoluteHttps(string value)
    {
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
        if (value.Contains('#'))
        {
            throw new InvalidOperationException("The payment URL must not contain a fragment.");
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
