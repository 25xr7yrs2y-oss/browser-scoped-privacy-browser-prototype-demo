using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrivacyBrowser.App;

public sealed class BackendController : IAsyncDisposable
{
    public const int ControlPort = 44050;
    public const int ProxyPort = 4449;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly AppOptions _options;
    private readonly HttpClient _client;
    private Process? _process;
    private bool _stopping;

    public BackendController(AppOptions options)
    {
        _options = options;
        _client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
        {
            BaseAddress = new Uri($"http://127.0.0.1:{ControlPort}/"),
            Timeout = TimeSpan.FromSeconds(15),
        };
    }

    public event Action<string>? Log;
    public event Action<BackendLifecycleState>? LifecycleChanged;
    public int? OwnedProcessId => _process?.Id;
    public BackendLifecycleState LifecycleState { get; private set; } = BackendLifecycleState.Stopped;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        SetLifecycle(BackendLifecycleState.Starting);
        try
        {
            if (_options.SkipBackendLaunch)
            {
                Log?.Invoke("Using an already-running backend as requested. Browser launch remains disabled because ownership cannot be verified.");
                await WaitUntilReadyAsync(cancellationToken);
                SetLifecycle(BackendLifecycleState.Running);
                return;
            }

            if (await IsHealthyAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    $"A Myst control endpoint is already listening on 127.0.0.1:{ControlPort}. " +
                    "Stop it first, or use --skip-backend-launch for diagnostics only.");
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

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log?.Invoke(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log?.Invoke(e.Data); };
            process.Exited += (_, _) =>
            {
                var exitCode = TryGetExitCode(process);
                Log?.Invoke($"Backend exited with code {exitCode}.");
                if (!_stopping) SetLifecycle(BackendLifecycleState.Crashed);
            };
            _process = process;

            if (!process.Start())
            {
                throw new InvalidOperationException("The Myst backend process could not be started.");
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Log?.Invoke($"Started Myst backend process {process.Id} without the Electron web UI.");
            await WaitUntilReadyAsync(cancellationToken);
            SetLifecycle(BackendLifecycleState.Running);
        }
        catch
        {
            SetLifecycle(BackendLifecycleState.Failed);
            throw;
        }
    }

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        if (_options.SkipBackendLaunch)
        {
            throw new InvalidOperationException("An adopted backend cannot be restarted safely. Start Privacy Browser without --skip-backend-launch.");
        }

        await StopOwnedProcessAsync(respectKeepRunning: false);
        await StartAsync(cancellationToken);
    }

    public async Task<BackendSnapshot> GetSnapshotAsync(
        string? selectedIdentityId = null,
        CancellationToken cancellationToken = default)
    {
        if (!await IsHealthyAsync(cancellationToken))
        {
            return BackendSnapshot.Offline();
        }

        // These resources have different upstream dependencies. Capture each result
        // independently so a slow registration RPC cannot make the whole UI appear offline.
        var connectionTask = CaptureAsync("connection", () => GetAsync<ConnectionInfo>("connection", cancellationToken), cancellationToken);
        var identitiesTask = CaptureAsync("identity", () => GetAsync<IdentityList>("identities", cancellationToken), cancellationToken);
        var termsTask = CaptureAsync("terms", () => GetAsync<TermsStatus>("terms", cancellationToken), cancellationToken);
        await Task.WhenAll(connectionTask, identitiesTask, termsTask);

        var issues = new List<BackendIssue>();
        AddIssue(connectionTask.Result, issues);
        AddIssue(identitiesTask.Result, issues);
        AddIssue(termsTask.Result, issues);

        var identities = new List<IdentityDetails>();
        var references = identitiesTask.Result.Value?.Identities
            .Where(identity => !string.IsNullOrWhiteSpace(identity.Id))
            .GroupBy(identity => identity.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray() ?? [];
        var detailTasks = references.Select(reference => CaptureAsync("identity status",
            () => GetAsync<IdentityDetails>($"identities/{Uri.EscapeDataString(reference.Id)}", cancellationToken),
            cancellationToken)).ToArray();
        await Task.WhenAll(detailTasks);
        for (var index = 0; index < detailTasks.Length; index++)
        {
            var identityResult = detailTasks[index].Result;
            AddIssue(identityResult, issues);
            identities.Add(identityResult.Value ?? new IdentityDetails
            {
                Id = references[index].Id,
                RegistrationStatus = "Unavailable",
            });
        }

        var resolvedSelection = identities.Any(identity =>
            identity.Id.Equals(selectedIdentityId, StringComparison.OrdinalIgnoreCase))
            ? selectedIdentityId
            : null;

        return new BackendSnapshot(
            true,
            connectionTask.Result.Value ?? new ConnectionInfo { Status = "STATUS_UNAVAILABLE" },
            identities,
            resolvedSelection,
            termsTask.Result.Value,
            issues,
            DateTimeOffset.Now);
    }

    public async Task<IReadOnlyList<ProviderProposal>> GetProvidersAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<ProposalList>("proposals?service_type=wireguard&access_policy=all", cancellationToken);
        return result.Proposals
            .Where(p => !string.IsNullOrWhiteSpace(p.ProviderId) &&
                        p.ServiceType.Equals("wireguard", StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.ProviderId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => p.Location.Country)
            .ThenBy(p => p.Location.City)
            .ThenBy(p => p.Price.PerGiBTokens.Value)
            .Take(500)
            .ToArray();
    }

    public Task<IdentityReference> CreateIdentityAsync(string passphrase, CancellationToken cancellationToken = default)
    {
        ValidatePassphrase(passphrase);
        return SendAsync<IdentityReference>(HttpMethod.Post, "identities", new { passphrase }, cancellationToken);
    }

    public async Task<IdentityReference> ImportIdentityAsync(
        byte[] encryptedKey,
        string currentPassphrase,
        CancellationToken cancellationToken = default)
    {
        if (encryptedKey.Length == 0) throw new InvalidOperationException("The selected identity file is empty.");
        ValidatePassphrase(currentPassphrase);
        return await SendAsync<IdentityReference>(HttpMethod.Post, "identities-import", new
        {
            data = encryptedKey,
            current_passphrase = currentPassphrase,
            new_passphrase = currentPassphrase,
            set_default = true,
        }, cancellationToken);
    }

    public Task UnlockIdentityAsync(string id, string passphrase, CancellationToken cancellationToken = default)
    {
        ValidatePassphrase(passphrase);
        return SendAsync(HttpMethod.Put, $"identities/{Uri.EscapeDataString(id)}/unlock", new { passphrase }, cancellationToken);
    }

    public async Task RegisterIdentityAsync(string id, string passphrase, CancellationToken cancellationToken = default)
    {
        await UnlockIdentityAsync(id, passphrase, cancellationToken);
        await SendAsync(HttpMethod.Post, $"identities/{Uri.EscapeDataString(id)}/register", new { }, cancellationToken);
    }

    public Task AcceptConsumerTermsAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            throw new InvalidOperationException("The backend did not report a current terms version.");
        }
        return SendAsync(HttpMethod.Post, "terms", new
        {
            agreed_consumer = true,
            agreed_version = currentVersion,
        }, cancellationToken);
    }

    public async Task<BalanceStatus> RefreshBalanceAsync(string identityId, CancellationToken cancellationToken = default)
    {
        return await SendAsync<BalanceStatus>(HttpMethod.Put,
            $"identities/{Uri.EscapeDataString(identityId)}/balance/refresh", null, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentGateway>> GetPaymentGatewaysAsync(CancellationToken cancellationToken = default)
    {
        var gateways = await GetAsync<List<PaymentGateway>>("v2/payment-order-gateways?options_currency=MYST", cancellationToken);
        return gateways
            .Where(g => PaymentTargetParser.SupportsGateway(g.Name) && g.Currencies.Count > 0)
            .ToArray();
    }

    public async Task<PaymentOrder> CreatePaymentOrderAsync(
        string identityId,
        PaymentGateway gateway,
        decimal mystAmount,
        string currency,
        string country,
        string state,
        CancellationToken cancellationToken = default)
    {
        if (!PaymentTargetParser.SupportsGateway(gateway.Name))
        {
            throw new InvalidOperationException("The selected payment gateway is not supported.");
        }
        if (mystAmount <= 0) throw new ArgumentOutOfRangeException(nameof(mystAmount), "Top-up amount must be greater than zero.");
        if (gateway.OrderOptions.Minimum > 0 && mystAmount <= gateway.OrderOptions.Minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(mystAmount),
                $"Top-up amount must be greater than {gateway.OrderOptions.Minimum:0.####} MYST.");
        }
        if (!gateway.Currencies.Contains(currency, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{gateway.DisplayName} does not accept {currency}.");
        }
        if (country.Length != 2) throw new InvalidOperationException("Country must be a two-letter code, such as US or DE.");
        if (!string.IsNullOrWhiteSpace(state) && state.Length != 2)
        {
            throw new InvalidOperationException("State must be a two-letter code or left blank.");
        }

        return await SendAsync<PaymentOrder>(HttpMethod.Post,
            $"v2/identities/{Uri.EscapeDataString(identityId)}/{Uri.EscapeDataString(gateway.Name)}/payment-order",
            new
            {
                myst_amount = mystAmount.ToString(CultureInfo.InvariantCulture),
                pay_currency = currency.ToUpperInvariant(),
                country = country.ToUpperInvariant(),
                state = state.ToUpperInvariant(),
                gateway_caller_data = new { },
            }, cancellationToken);
    }

    public async Task ConnectAsync(
        string identityId,
        string passphrase,
        ProviderProposal provider,
        CancellationToken cancellationToken = default)
    {
        await UnlockIdentityAsync(identityId, passphrase, cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(75));
        await SendAsync(HttpMethod.Put, "connection", new
        {
            consumer_id = identityId,
            provider_id = provider.ProviderId,
            service_type = string.IsNullOrWhiteSpace(provider.ServiceType) ? "wireguard" : provider.ServiceType,
            filter = new { include_monitoring_failed = true },
            connect_options = new
            {
                dns = "provider",
                kill_switch = true,
                proxy_port = ProxyPort,
            },
        }, timeout.Token);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, "connection", null, cancellationToken);

    public bool IsOwnedProxyListening()
    {
        if (_process is null)
        {
            throw new InvalidOperationException(
                "Browser launch is disabled for an adopted backend because proxy process ownership cannot be verified.");
        }
        var listeners = TcpListenerInspector.GetListeners(ProxyPort);
        if (listeners.Count == 0) return false;
        if (listeners.Any(l => !IPAddress.IsLoopback(l.Address)))
        {
            throw new InvalidOperationException($"Proxy port {ProxyPort} has a non-loopback listener.");
        }
        if (listeners.Any(l => l.ProcessId != _process.Id))
        {
            throw new InvalidOperationException($"Proxy port {ProxyPort} is owned by an unexpected process.");
        }
        return true;
    }

    public async Task StopAsync()
    {
        await StopOwnedProcessAsync(respectKeepRunning: true);
    }

    private async Task StopOwnedProcessAsync(bool respectKeepRunning)
    {
        if ((respectKeepRunning && _options.KeepBackendRunning) || _process is null) return;
        _stopping = true;

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
        finally
        {
            _process.Dispose();
            _process = null;
            _stopping = false;
            SetLifecycle(BackendLifecycleState.Stopped);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
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
        using var response = await SendCoreAsync(method, path, body, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(method, path, body, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Backend returned an empty response for {path}.");
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        }
        return await _client.SendAsync(request, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw BackendErrorTranslator.FromResponse(response.StatusCode, response.ReasonPhrase, detail);
    }

    private static async Task<ApiResult<T>> CaptureAsync<T>(
        string area,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return new ApiResult<T>(await operation(), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ApiResult<T>(default, new BackendIssue(area, BackendErrorTranslator.ToUserMessage(ex)));
        }
    }

    private static void AddIssue<T>(ApiResult<T> result, ICollection<BackendIssue> issues)
    {
        if (result.Issue is not null) issues.Add(result.Issue);
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

    private void SetLifecycle(BackendLifecycleState state)
    {
        LifecycleState = state;
        LifecycleChanged?.Invoke(state);
    }

    private static string TryGetExitCode(Process process)
    {
        try { return process.ExitCode.ToString(CultureInfo.InvariantCulture); }
        catch (InvalidOperationException) { return "unknown"; }
    }

    private static void ValidatePassphrase(string passphrase)
    {
        if (string.IsNullOrEmpty(passphrase))
        {
            throw new InvalidOperationException("Enter the identity passphrase and try again.");
        }
    }

    private sealed record ApiResult<T>(T? Value, BackendIssue? Issue);
}
