using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace PrivacyBrowser.App;

public sealed class BrowserLauncher
{
    private readonly AppOptions _options;
    private readonly BackendController _backend;
    private Process? _browserProcess;

    public BrowserLauncher(AppOptions options, BackendController backend)
    {
        _options = options;
        _backend = backend;
    }

    public event Action<int>? BrowserExited;

    public bool IsBrowserRunning
    {
        get
        {
            try { return _browserProcess is { HasExited: false }; }
            catch (InvalidOperationException) { return false; }
        }
    }

    public BrowserReadiness EvaluateReadiness(BackendSnapshot snapshot)
    {
        var issues = new List<string>();
        if (!File.Exists(_options.BrowserExe))
        {
            issues.Add("Mullvad Browser is missing from the application bundle.");
        }

        var policySource = Path.Combine(_options.BundleRoot, "config", "policies.json");
        if (!File.Exists(policySource))
        {
            issues.Add("The locked browser policy is missing from the application bundle.");
        }
        else if (!PolicyHasRequiredPrivacySettings(policySource))
        {
            issues.Add("The browser policy does not enforce the expected loopback proxy and privacy settings.");
        }

        if (File.Exists(_options.BrowserExe))
        {
            var targetPolicy = Path.Combine(Path.GetDirectoryName(_options.BrowserExe)!, "distribution", "policies.json");
            try
            {
                if (File.Exists(targetPolicy) && File.Exists(policySource) && !HashesMatch(policySource, targetPolicy))
                {
                    issues.Add("The browser contains an unrelated or modified policy file.");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                issues.Add("The installed browser policy could not be verified.");
            }
        }

        if (!snapshot.IsConnected)
        {
            issues.Add("Connect to a registered provider before launching the browser.");
        }
        else
        {
            try
            {
                if (!_backend.IsOwnedProxyListening())
                {
                    issues.Add($"The app-owned proxy is not listening on 127.0.0.1:{BackendController.ProxyPort}.");
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
            {
                issues.Add(ex.Message);
            }
        }

        if (IsBrowserRunning && issues.Count == 0)
        {
            return new BrowserReadiness(BrowserReadinessState.BrowserRunning,
                "The isolated browser is running.", issues);
        }

        if (issues.Count == 0)
        {
            return new BrowserReadiness(BrowserReadinessState.Ready,
                "Ready. The app-owned proxy and locked browser policy are verified.", []);
        }

        var hasConfigurationError = issues.Any(issue =>
            !issue.StartsWith("Connect to", StringComparison.OrdinalIgnoreCase) &&
            !issue.Contains("not listening", StringComparison.OrdinalIgnoreCase));
        return new BrowserReadiness(
            hasConfigurationError ? BrowserReadinessState.Error : BrowserReadinessState.Incomplete,
            issues[0],
            issues);
    }

    public Process Launch(BackendSnapshot snapshot)
    {
        var readiness = EvaluateReadiness(snapshot);
        if (!readiness.CanLaunch)
        {
            throw new InvalidOperationException(readiness.Summary);
        }

        InstallPolicy();
        Directory.CreateDirectory(_options.ProfilePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.BrowserExe,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "-no-remote", "-new-instance", "-profile", _options.ProfilePath })
        {
            startInfo.ArgumentList.Add(argument);
        }
        foreach (var argument in _options.AdditionalBrowserArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.ArgumentList.Add(_options.InitialUrl);
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Mullvad Browser could not be started.");
        _browserProcess = process;
        process.Exited += (_, _) =>
        {
            BrowserExited?.Invoke(process.Id);
            process.Dispose();
            if (ReferenceEquals(_browserProcess, process)) _browserProcess = null;
        };
        process.EnableRaisingEvents = true;
        return process;
    }

    private void InstallPolicy()
    {
        var source = Path.Combine(_options.BundleRoot, "config", "policies.json");
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("Locked browser policy was not found.", source);
        }

        var distribution = Path.Combine(Path.GetDirectoryName(_options.BrowserExe)!, "distribution");
        var target = Path.Combine(distribution, "policies.json");
        Directory.CreateDirectory(distribution);
        if (File.Exists(target) && !HashesMatch(source, target))
        {
            throw new InvalidOperationException($"Refusing to overwrite an unrelated browser policy at {target}.");
        }
        File.Copy(source, target, overwrite: true);
    }

    private static bool HashesMatch(string left, string right)
    {
        using var sha = SHA256.Create();
        using var leftStream = File.OpenRead(left);
        var leftHash = sha.ComputeHash(leftStream);
        using var rightStream = File.OpenRead(right);
        var rightHash = sha.ComputeHash(rightStream);
        return leftHash.SequenceEqual(rightHash);
    }

    private static bool PolicyHasRequiredPrivacySettings(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var policies = document.RootElement.GetProperty("policies");
            var proxy = policies.GetProperty("Proxy");
            if (!string.Equals(proxy.GetProperty("Mode").GetString(), "manual", StringComparison.OrdinalIgnoreCase) ||
                proxy.GetProperty("HTTPProxy").GetString() != $"127.0.0.1:{BackendController.ProxyPort}" ||
                proxy.GetProperty("SSLProxy").GetString() != $"127.0.0.1:{BackendController.ProxyPort}" ||
                proxy.GetProperty("Locked").GetBoolean() != true)
            {
                return false;
            }

            var preferences = policies.GetProperty("Preferences");
            return IsLockedValue(preferences, "network.trr.mode", value => value.GetInt32() == 5) &&
                IsLockedValue(preferences, "network.dns.disablePrefetch", value => value.GetBoolean()) &&
                IsLockedValue(preferences, "media.peerconnection.enabled", value => !value.GetBoolean());
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsLockedValue(
        JsonElement preferences,
        string name,
        Func<JsonElement, bool> predicate)
    {
        if (!preferences.TryGetProperty(name, out var setting) ||
            !setting.TryGetProperty("Status", out var status) ||
            !string.Equals(status.GetString(), "locked", StringComparison.OrdinalIgnoreCase) ||
            !setting.TryGetProperty("Value", out var value))
        {
            return false;
        }
        return predicate(value);
    }
}
