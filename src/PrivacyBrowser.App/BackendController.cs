using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PrivacyBrowser.App;

public sealed class BackendController : IAsyncDisposable
{
    public const int ControlPort = 44050;
    public const int ProxyPort = 4449;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppOptions _options;
    private readonly HttpClient _client;
    private Process? _process;

    public BackendController(AppOptions options)
    {
        _options = options;
        _client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
        {
            BaseAddress = new Uri($"http://127.0.0.1:{ControlPort}/"),
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    public event Action<string>? Log;
    public int? OwnedProcessId => _process?.Id;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_options.SkipBackendLaunch)
        {
            Log?.Invoke("Using an already-running backend as requested.");
            await WaitUntilReadyAsync(cancellationToken);
            return;
        }

        if (await IsHealthyAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"A Myst control endpoint is already listening on 127.0.0.1:{ControlPort}. " +
                "Stop it first, or use --skip-backend-launch to adopt it explicitly.");
        }

        var nodeExe = ResolveNodeExecutable(_options.BackendExe);
        var startInfo = new ProcessStartInfo
        {
            FileName = nodeExe,
            WorkingDirectory = Path.GetDirectoryName(nodeExe)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[]
        {
            "--ui.enable=false",
            "--usermode",
            "--proxymode",
            "--proxy.bind.address=127.0.0.1",
            "--consumer",
            "--tequilapi.address=127.0.0.1",
            $"--tequilapi.port={ControlPort}",
            "--discovery.type=api",
            "daemon",
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log?.Invoke(e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log?.Invoke(e.Data); };
        _process.Exited += (_, _) => Log?.Invoke($"Backend exited with code {_process?.ExitCode}.");

        if (!_process.Start())
        {
            throw new InvalidOperationException("The Myst backend process could not be started.");
        }
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        Log?.Invoke($"Started Myst backend process {_process.Id} without the Electron web UI.");
        await WaitUntilReadyAsync(cancellationToken);
    }

    public async Task<BackendSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsHealthyAsync(cancellationToken))
        {
            return new BackendSnapshot(false, "BACKEND_OFFLINE", null, null);
        }

        var connection = await GetAsync<ConnectionInfo>("connection", cancellationToken);
        var identities = await GetAsync<IdentityList>("identities", cancellationToken);
        var terms = await GetAsync<TermsStatus>("terms", cancellationToken);
        IdentityDetails? identity = null;
        var id = identities.Identities.FirstOrDefault()?.Id;
        if (!string.IsNullOrWhiteSpace(id))
        {
            identity = await GetAsync<IdentityDetails>($"identities/{Uri.EscapeDataString(id)}", cancellationToken);
        }
        return new BackendSnapshot(true, connection.Status, identity, terms);
    }

    public async Task<IReadOnlyList<ProviderProposal>> GetProvidersAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<ProposalList>("proposals?service_type=wireguard", cancellationToken);
        return result.Proposals
            .OrderByDescending(p => p.Location.Country)
            .ThenBy(p => p.Location.City)
            .Take(250)
            .ToArray();
    }

    public async Task CreateIdentityAsync(CancellationToken cancellationToken = default)
    {
        await SendAsync(HttpMethod.Post, "identities", new { passphrase = "" }, cancellationToken);
    }

    public async Task RegisterIdentityAsync(string id, CancellationToken cancellationToken = default)
    {
        await SendAsync(HttpMethod.Put, $"identities/{Uri.EscapeDataString(id)}/unlock", new { passphrase = "" }, cancellationToken);
        await SendAsync(HttpMethod.Post, $"identities/{Uri.EscapeDataString(id)}/register", new { }, cancellationToken);
    }

    public async Task AcceptConsumerTermsAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            throw new InvalidOperationException("The backend did not report a current terms version.");
        }
        await SendAsync(HttpMethod.Post, "terms", new
        {
            agreed_consumer = true,
            agreed_version = currentVersion,
        }, cancellationToken);
    }

    public async Task ConnectAsync(string identityId, ProviderProposal provider, CancellationToken cancellationToken = default)
    {
        await SendAsync(HttpMethod.Put, $"identities/{Uri.EscapeDataString(identityId)}/unlock", new { passphrase = "" }, cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(75));
        await SendAsync(HttpMethod.Put, "connection", new
        {
            consumer_id = identityId,
            provider_id = provider.ProviderId,
            service_type = string.IsNullOrWhiteSpace(provider.ServiceType) ? "wireguard" : provider.ServiceType,
            connect_options = new
            {
                dns = "provider",
                disable_kill_switch = true,
                proxy_port = ProxyPort,
            },
        }, timeout.Token);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, "connection", null, cancellationToken);

    public bool IsOwnedProxyListening()
    {
        var listeners = TcpListenerInspector.GetListeners(ProxyPort);
        if (listeners.Count == 0)
        {
            return false;
        }
        if (listeners.Any(l => !IPAddress.IsLoopback(l.Address)))
        {
            throw new InvalidOperationException($"Proxy port {ProxyPort} has a non-loopback listener.");
        }
        if (_process is not null && listeners.Any(l => l.ProcessId != _process.Id))
        {
            throw new InvalidOperationException($"Proxy port {ProxyPort} is owned by an unexpected process.");
        }
        return true;
    }

    public async Task StopAsync()
    {
        if (_options.KeepBackendRunning || _process is null)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await SendAsync(HttpMethod.Post, "stop", null, timeout.Token);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Graceful backend shutdown failed: {ex.Message}");
        }

        try
        {
            if (!_process.HasExited && !await WaitForExitAsync(_process, TimeSpan.FromSeconds(5)))
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The owned process exited between checks.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _process?.Dispose();
        _client.Dispose();
    }

    private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_process is { HasExited: true })
            {
                throw new InvalidOperationException($"The Myst backend exited before it became ready (exit code {_process.ExitCode}).");
            }
            if (await IsHealthyAsync(cancellationToken))
            {
                Log?.Invoke("Myst backend control endpoint is ready on loopback port 44050.");
                return;
            }
            await Task.Delay(1000, cancellationToken);
        }
        throw new TimeoutException("The Myst backend did not become ready within 30 seconds.");
    }

    private async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(1500));
            using var response = await _client.GetAsync("healthcheck", timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Backend returned an empty response for {path}.");
    }

    private async Task SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        }
        using var response = await _client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        if (detail.Length > 600) detail = detail[..600];
        throw new InvalidOperationException(
            $"Backend request failed ({(int)response.StatusCode} {response.ReasonPhrase})" +
            (string.IsNullOrWhiteSpace(detail) ? "." : $": {detail}"));
    }

    private static string ResolveNodeExecutable(string configuredPath)
    {
        if (!File.Exists(configuredPath))
        {
            throw new FileNotFoundException("Backend executable was not found.", configuredPath);
        }
        if (Path.GetFileName(configuredPath).Equals("myst.exe", StringComparison.OrdinalIgnoreCase))
        {
            return configuredPath;
        }

        var root = Path.GetDirectoryName(configuredPath)!;
        var node = Path.Combine(root, "resources", "app.asar.unpacked", "node_modules",
            "@mysteriumnetwork", "node", "bin", "win", "x64", "myst.exe");
        if (!File.Exists(node))
        {
            throw new FileNotFoundException(
                "The unpacked Myst node executable was not found. Supply --backend-exe with MysteriumVPN.exe or myst.exe.", node);
        }
        return node;
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
