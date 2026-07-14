using System.Diagnostics;
using System.Security.Cryptography;

namespace PrivacyBrowser.App;

public sealed class BrowserLauncher
{
    private readonly AppOptions _options;
    private readonly BackendController _backend;

    public BrowserLauncher(AppOptions options, BackendController backend)
    {
        _options = options;
        _backend = backend;
    }

    public Process Launch()
    {
        if (!File.Exists(_options.BrowserExe))
        {
            throw new FileNotFoundException("Mullvad Browser executable was not found.", _options.BrowserExe);
        }
        if (!_backend.IsOwnedProxyListening())
        {
            throw new InvalidOperationException("The owned backend proxy is not listening on 127.0.0.1:4449.");
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
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Mullvad Browser could not be started.");
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
}
