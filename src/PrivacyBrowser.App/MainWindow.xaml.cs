using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PrivacyBrowser.App;

public partial class MainWindow : Window
{
    private readonly BackendController _backend;
    private readonly BrowserLauncher _browser;
    private readonly DispatcherTimer _timer;
    private BackendSnapshot _snapshot = BackendSnapshot.Offline("The native controller is starting.");
    private bool _busy;
    private bool _refreshing;
    private bool _closing;
    private string _lastIssueSignature = "";

    public MainWindow(AppOptions options)
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.1";
        Title = $"Privacy Browser {version}";
        _backend = new BackendController(options);
        _browser = new BrowserLauncher(options, _backend);
        _backend.Log += message => Dispatcher.BeginInvoke(() =>
        {
            var friendly = BackendErrorTranslator.ToActivityMessage(message);
            if (friendly is not null) AppendActivity(friendly);
        });
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += async (_, _) => await RefreshSnapshotAsync(showErrors: false);
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("Starting the private backend…", "Backend is ready.", async () =>
        {
            await _backend.StartAsync();
            await RefreshSnapshotAsync(showErrors: true);
        });
        _timer.Start();

        if (_snapshot.NodeUp)
        {
            await RunOperationAsync("Discovering WireGuard providers…", "Provider list is ready.", RefreshProvidersAsync);
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closing) return;
        e.Cancel = true;
        _closing = true;
        _timer.Stop();
        IsEnabled = false;
        FooterStatusText.Text = "Stopping the owned backend…";
        await _backend.DisposeAsync();
        Close();
    }

    private void ControlsButton_Click(object sender, RoutedEventArgs e) =>
        SetControls(ControlPanel.Visibility != Visibility.Visible);

    private void OpenControlsButton_Click(object sender, RoutedEventArgs e) => SetControls(true);

    private void SetControls(bool open)
    {
        ControlPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        ControlColumn.Width = open ? new GridLength(440) : new GridLength(0);
        ControlsButton.Content = open ? "Close controls" : "Controls";
    }

    private async void CreateIdentityButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("Creating a local identity…", "Identity created.", async () =>
        {
            await _backend.CreateIdentityAsync();
            AppendActivity("A local Mysterium identity was created.");
            await RefreshSnapshotAsync(true);
        });
    }

    private async void AcceptTermsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (AcceptTermsCheckBox.IsChecked != true || _snapshot.Terms is null)
        {
            AcceptTermsCheckBox.IsChecked = _snapshot.Terms?.IsCurrent == true;
            return;
        }

        await RunOperationAsync("Saving terms acceptance…", "Consumer terms accepted.", async () =>
        {
            await _backend.AcceptConsumerTermsAsync(_snapshot.Terms.CurrentVersion);
            AppendActivity($"Accepted Mysterium consumer terms {_snapshot.Terms.CurrentVersion}.");
            await RefreshSnapshotAsync(true);
        });
        AcceptTermsCheckBox.IsChecked = _snapshot.Terms?.IsCurrent == true;
    }

    private async void RegisterIdentityButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot.Identity is null) return;
        await RunOperationAsync("Requesting identity registration…", "Registration requested. Refresh status as it progresses.", async () =>
        {
            await _backend.RegisterIdentityAsync(_snapshot.Identity.Id);
            AppendActivity("Identity registration started. A wallet top-up may be required to finish registration.");
            await RefreshSnapshotAsync(true);
        });
    }

    private async void RefreshIdentityButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("Refreshing identity status…", "Identity status refreshed.",
            () => RefreshSnapshotAsync(showErrors: true));
    }

    private async void RefreshBalanceButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot.Identity is null) return;
        await RunOperationAsync("Refreshing MYST balance…", "Wallet balance refreshed.", async () =>
        {
            var balance = await _backend.RefreshBalanceAsync(_snapshot.Identity.Id);
            AppendActivity($"Wallet balance refreshed: {balance.BalanceTokens.Display} MYST.");
            await RefreshSnapshotAsync(true);
        });
    }

    private async void TopUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot.Identity is null) return;
        var window = new TopUpWindow(_backend, _snapshot.Identity.Id) { Owner = this };
        window.ShowDialog();
        if (window.CreatedOrder is not null)
        {
            AppendActivity($"Payment order {window.CreatedOrder.Id} created; status {window.CreatedOrder.Status}.");
            await RefreshSnapshotAsync(showErrors: false);
        }
    }

    private async void RefreshProvidersButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("Discovering WireGuard providers…", "Provider list refreshed.", RefreshProvidersAsync);
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot.Identity is null || ProviderComboBox.SelectedItem is not ProviderProposal provider) return;
        await RunOperationAsync($"Connecting to {provider.DisplayName}…", "Provider connected. The browser is ready.", async () =>
        {
            AppendActivity($"Connecting to {provider.DisplayName}.");
            await _backend.ConnectAsync(_snapshot.Identity.Id, provider);
            await RefreshSnapshotAsync(true);
        });
    }

    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("Disconnecting from the provider…", "Provider disconnected.", async () =>
        {
            await _backend.DisconnectAsync();
            AppendActivity("Provider connection was disconnected.");
            await RefreshSnapshotAsync(true);
        });
    }

    private void LaunchBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process process = _browser.Launch();
            AppendActivity($"Privacy browser started as process {process.Id}.");
            ShowOperation("Privacy browser launched.", OperationKind.Success);
        }
        catch (Exception ex)
        {
            ShowOperationError(ex);
        }
    }

    private void ProviderComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RenderSelectedProvider();
        UpdateControls();
    }

    private void ClearActivityButton_Click(object sender, RoutedEventArgs e) => ActivityText.Clear();

    private async Task RefreshProvidersAsync()
    {
        var selectedId = (ProviderComboBox.SelectedItem as ProviderProposal)?.ProviderId;
        var providers = await _backend.GetProvidersAsync();
        ProviderComboBox.ItemsSource = providers;
        ProviderComboBox.SelectedItem = providers.FirstOrDefault(p => p.ProviderId == selectedId) ?? providers.FirstOrDefault();
        ProviderCountText.Text = providers.Count == 1 ? "1 provider" : $"{providers.Count} providers";
        AppendActivity(providers.Count == 0
            ? "No WireGuard providers were returned. Check network access and refresh again."
            : $"Loaded {providers.Count} WireGuard providers.");
        RenderSelectedProvider();
        UpdateControls();
    }

    private async Task RefreshSnapshotAsync(bool showErrors)
    {
        if (_refreshing || _closing) return;
        _refreshing = true;
        try
        {
            _snapshot = await _backend.GetSnapshotAsync();
            RenderSnapshot();
        }
        catch (Exception ex)
        {
            if (showErrors) ShowOperationError(ex);
            FooterStatusText.Text = "Backend status is temporarily unavailable.";
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RenderSnapshot()
    {
        var connected = _snapshot.IsConnected;
        var statusUnavailable = _snapshot.Connection.Status.Equals("STATUS_UNAVAILABLE", StringComparison.OrdinalIgnoreCase);

        if (!_snapshot.NodeUp)
        {
            ConnectionStatusText.Text = "Backend offline";
            ConnectionDetailText.Text = "The native controller cannot reach the Myst backend. Open Controls for details.";
            ConnectionStatusDot.Fill = Brush("Danger");
            MainBackendText.Text = "Offline";
            MainBackendDetailText.Text = "Control service unavailable";
        }
        else if (connected)
        {
            ConnectionStatusText.Text = "Privacy is active";
            ConnectionDetailText.Text = "The browser-scoped provider proxy is ready.";
            ConnectionStatusDot.Fill = Brush("Success");
            MainBackendText.Text = "Online";
            MainBackendDetailText.Text = "Myst control is ready";
        }
        else if (statusUnavailable)
        {
            ConnectionStatusText.Text = "Status unavailable";
            ConnectionDetailText.Text = "The backend is online, but one status service did not respond. You can retry from Controls.";
            ConnectionStatusDot.Fill = Brush("Warning");
            MainBackendText.Text = "Partially online";
            MainBackendDetailText.Text = "Some data unavailable";
        }
        else
        {
            ConnectionStatusText.Text = "Not connected";
            ConnectionDetailText.Text = "Complete the identity and wallet steps, then choose a provider.";
            ConnectionStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x8A, 0x97, 0x92));
            MainBackendText.Text = "Online";
            MainBackendDetailText.Text = "Waiting for connection";
        }

        var activeProvider = _snapshot.Connection.Proposal;
        ConnectedProviderText.Text = activeProvider is null
            ? "No active provider"
            : $"Provider: {activeProvider.DisplayName}";

        if (_snapshot.Identity is null)
        {
            IdentityAddressText.Text = "No identity created";
            IdentityStatusBadgeText.Text = "Not created";
            IdentityStatusBadge.Background = new SolidColorBrush(Color.FromRgb(0xED, 0xF1, 0xF0));
            IdentityHelpText.Text = "Accept the terms, then create an identity to begin.";
            MainIdentityText.Text = "Not created";
            MainIdentityDetailText.Text = "Identity required";
            WalletBalanceText.Text = "—";
            WalletPaymentStatusText.Text = "Create an identity to view payment status.";
            MainBalanceText.Text = "— MYST";
            MainPaymentText.Text = "Identity required";
        }
        else
        {
            var identity = _snapshot.Identity;
            IdentityAddressText.Text = identity.Id;
            IdentityStatusBadgeText.Text = FormatRegistrationStatus(identity.RegistrationStatus);
            MainIdentityText.Text = FormatRegistrationStatus(identity.RegistrationStatus);
            MainIdentityDetailText.Text = ShortId(identity.Id);
            IdentityHelpText.Text = IdentityGuidance(identity);
            SetIdentityBadge(identity);

            var balance = identity.BalanceTokens.Display;
            WalletBalanceText.Text = $"{balance} MYST";
            MainBalanceText.Text = $"{balance} MYST";
            var paymentStatus = PaymentGuidance(identity);
            WalletPaymentStatusText.Text = paymentStatus;
            MainPaymentText.Text = paymentStatus;
        }

        AcceptTermsCheckBox.IsChecked = _snapshot.Terms?.IsCurrent == true;
        TermsVersionText.Text = _snapshot.Terms is null
            ? "Terms status is unavailable."
            : $"Current version: {_snapshot.Terms.CurrentVersion}";

        BrowserReadinessText.Text = connected
            ? "Ready. Browser traffic will use the selected provider with no direct fallback."
            : "Connect to a provider before launching the browser.";

        FooterStatusText.Text = !_snapshot.NodeUp
            ? "Native UI · Waiting for backend"
            : _snapshot.Issues.Count > 0
                ? $"Native UI · Backend online · {_snapshot.Issues.Count} status item(s) need attention"
                : $"Native UI · Control 127.0.0.1:{BackendController.ControlPort} · Proxy 127.0.0.1:{BackendController.ProxyPort}";
        LastUpdatedText.Text = $"Updated {_snapshot.ObservedAt:HH:mm:ss}";

        var issueSignature = string.Join('|', _snapshot.Issues.Select(i => $"{i.Area}:{i.Message}"));
        if (issueSignature.Length > 0 && issueSignature != _lastIssueSignature)
        {
            foreach (var issue in _snapshot.Issues)
            {
                AppendActivity($"{Capitalize(issue.Area)}: {issue.Message}");
            }
        }
        _lastIssueSignature = issueSignature;

        RenderSelectedProvider();
        UpdateControls();
    }

    private void RenderSelectedProvider()
    {
        var provider = _snapshot.Connection.Proposal ?? ProviderComboBox.SelectedItem as ProviderProposal;
        if (provider is null)
        {
            ProviderDetailText.Text = "Refresh to discover available WireGuard providers.";
            MainProviderText.Text = "None";
            MainProviderDetailText.Text = "Choose in Controls";
            return;
        }

        var type = string.IsNullOrWhiteSpace(provider.Location.IpType) ? "Network type unknown" : provider.Location.IpType;
        ProviderDetailText.Text = $"{type} · {provider.PriceSummary}";
        MainProviderText.Text = provider.Location.Country.Length > 0 ? provider.Location.Country : ShortId(provider.ProviderId);
        MainProviderDetailText.Text = _snapshot.IsConnected ? "Connected" : provider.PriceSummary;
    }

    private void UpdateControls()
    {
        var identity = _snapshot.Identity;
        var hasIdentity = identity is not null;
        var termsAccepted = _snapshot.Terms?.IsCurrent == true;
        var registrationReady = identity is not null && (identity.IsRegistered || identity.RegistrationInProgress);
        var registrationKnown = identity is not null &&
            !identity.RegistrationStatus.Equals("Unavailable", StringComparison.OrdinalIgnoreCase);
        var hasProvider = ProviderComboBox.SelectedItem is ProviderProposal;
        var available = !_busy && _snapshot.NodeUp;

        AcceptTermsCheckBox.IsEnabled = available && !termsAccepted && _snapshot.Terms is not null;
        CreateIdentityButton.IsEnabled = available && termsAccepted && !hasIdentity;
        RegisterIdentityButton.IsEnabled = available && termsAccepted && hasIdentity && registrationKnown && !registrationReady;
        RefreshIdentityButton.IsEnabled = available;
        RefreshBalanceButton.IsEnabled = available && hasIdentity;
        TopUpButton.IsEnabled = available && registrationReady;
        RefreshProvidersButton.IsEnabled = available;
        ProviderComboBox.IsEnabled = available && !_snapshot.IsConnected;
        ConnectButton.IsEnabled = available && termsAccepted && registrationReady && hasProvider && !_snapshot.IsConnected;
        DisconnectButton.IsEnabled = !_busy && _snapshot.IsConnected;
        DisconnectMainButton.IsEnabled = !_busy && _snapshot.IsConnected;
        LaunchBrowserButton.IsEnabled = !_busy && _snapshot.IsConnected;

        ConnectionPrerequisiteText.Text = ConnectionPrerequisite(termsAccepted, hasIdentity, registrationReady, hasProvider);
    }

    private string ConnectionPrerequisite(bool termsAccepted, bool hasIdentity, bool registrationReady, bool hasProvider)
    {
        if (!_snapshot.NodeUp) return "Backend is offline.";
        if (_snapshot.IsConnected) return "Connected. Disconnect before changing provider.";
        if (!termsAccepted) return "Accept the consumer terms before connecting.";
        if (!hasIdentity) return "Create an identity before connecting.";
        if (!registrationReady) return "Register the identity before connecting.";
        if (!hasProvider) return "Choose a provider before connecting.";
        if (_snapshot.Identity?.BalanceTokens.Value <= 0) return "Ready, but the wallet shows no MYST; paid traffic may fail.";
        return "Ready to connect.";
    }

    private async Task RunOperationAsync(string progressMessage, string successMessage, Func<Task> action)
    {
        if (_busy || _closing) return;
        _busy = true;
        ShowOperation(progressMessage, OperationKind.Progress);
        FooterStatusText.Text = progressMessage;
        UpdateControls();
        try
        {
            await action();
            ShowOperation(successMessage, OperationKind.Success);
        }
        catch (Exception ex)
        {
            ShowOperationError(ex);
        }
        finally
        {
            _busy = false;
            UpdateControls();
        }
    }

    private void ShowOperationError(Exception exception)
    {
        var message = BackendErrorTranslator.ToUserMessage(exception);
        AppendActivity($"Could not complete operation: {message}");
        ShowOperation(message, OperationKind.Error);
        SetControls(true);
    }

    private void ShowOperation(string message, OperationKind kind)
    {
        OperationStatusText.Text = message;
        OperationStatusBorder.Visibility = Visibility.Visible;
        OperationProgressBar.Visibility = kind == OperationKind.Progress ? Visibility.Visible : Visibility.Collapsed;
        switch (kind)
        {
            case OperationKind.Success:
                OperationStatusBorder.Background = Brush("SuccessSoft");
                OperationStatusText.Foreground = Brush("Success");
                break;
            case OperationKind.Error:
                OperationStatusBorder.Background = Brush("DangerSoft");
                OperationStatusText.Foreground = Brush("Danger");
                break;
            default:
                OperationStatusBorder.Background = Brush("PrimarySoft");
                OperationStatusText.Foreground = Brush("Primary");
                break;
        }
    }

    private void AppendActivity(string message)
    {
        ActivityText.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        ActivityText.ScrollToEnd();
    }

    private void SetIdentityBadge(IdentityDetails identity)
    {
        if (identity.IsRegistered)
        {
            IdentityStatusBadge.Background = Brush("SuccessSoft");
            IdentityStatusBadgeText.Foreground = Brush("Success");
        }
        else if (identity.RegistrationInProgress)
        {
            IdentityStatusBadge.Background = Brush("WarningSoft");
            IdentityStatusBadgeText.Foreground = Brush("Warning");
        }
        else
        {
            IdentityStatusBadge.Background = Brush("DangerSoft");
            IdentityStatusBadgeText.Foreground = Brush("Danger");
        }
    }

    private static string IdentityGuidance(IdentityDetails identity)
    {
        if (identity.IsRegistered) return "Registered and available for provider connections.";
        if (identity.RegistrationInProgress) return "Registration is in progress. Top up if payment is required, then refresh.";
        if (identity.RegistrationStatus.Equals("Unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return "Registration could not be checked. Verify network access and refresh status.";
        }
        return "This identity must be registered before connecting.";
    }

    private static string PaymentGuidance(IdentityDetails identity)
    {
        if (identity.RegistrationInProgress) return "Registration pending · top up may be required";
        if (!identity.IsRegistered) return "Register the identity to enable payments";
        return identity.BalanceTokens.Value <= 0
            ? "No funds · top up before paid browsing"
            : "Funded · available for paid provider traffic";
    }

    private static string FormatRegistrationStatus(string status) => status switch
    {
        "InProgress" => "In progress",
        "RegistrationError" => "Registration error",
        "" => "Unknown",
        _ => status,
    };

    private SolidColorBrush Brush(string key) => (SolidColorBrush)FindResource(key);

    private static string ShortId(string id) => id.Length > 16 ? id[..10] + "…" + id[^4..] : id;
    private static string Capitalize(string value) => string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private enum OperationKind
    {
        Progress,
        Success,
        Error,
    }
}
