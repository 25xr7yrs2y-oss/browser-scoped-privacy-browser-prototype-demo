using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace PrivacyBrowser.App;

public partial class MainWindow : Window
{
    private readonly BackendController _backend;
    private readonly BrowserLauncher _browser;
    private readonly DispatcherTimer _timer;
    private BackendSnapshot _snapshot = new(false, "STARTING", null, null);
    private bool _busy;
    private bool _refreshing;
    private bool _closing;

    public MainWindow(AppOptions options)
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.1";
        Title = $"Privacy Browser {version}";
        _backend = new BackendController(options);
        _browser = new BrowserLauncher(options, _backend);
        _backend.Log += message => Dispatcher.Invoke(() => AppendActivity(message));
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await RefreshSnapshotAsync(showErrors: false);
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Starting backend…", async () =>
        {
            await _backend.StartAsync();
            await RefreshSnapshotAsync(showErrors: true);
        });
        _timer.Start();
        if (_snapshot.NodeUp)
        {
            await RunBusyAsync("Refreshing providers…", RefreshProvidersAsync);
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closing)
        {
            return;
        }

        e.Cancel = true;
        _closing = true;
        _timer.Stop();
        IsEnabled = false;
        FooterStatusText.Text = "Stopping the owned backend…";
        await _backend.DisposeAsync();
        Close();
    }

    private void ControlsButton_Click(object sender, RoutedEventArgs e)
    {
        var open = ControlPanel.Visibility != Visibility.Visible;
        ControlPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        ControlColumn.Width = open ? new GridLength(350) : new GridLength(0);
        ControlsButton.Content = open ? "Close controls" : "Controls";
    }

    private async void CreateIdentityButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Creating identity…", async () =>
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

        await RunBusyAsync("Saving terms acceptance…", async () =>
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
        await RunBusyAsync("Registering identity…", async () =>
        {
            await _backend.RegisterIdentityAsync(_snapshot.Identity.Id);
            AppendActivity("Identity registration was requested.");
            await RefreshSnapshotAsync(true);
        });
    }

    private async void RefreshProvidersButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Refreshing providers…", RefreshProvidersAsync);
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot.Identity is null || ProviderComboBox.SelectedItem is not ProviderProposal provider) return;
        await RunBusyAsync("Connecting to provider…", async () =>
        {
            AppendActivity($"Connecting to {provider.DisplayName}.");
            await _backend.ConnectAsync(_snapshot.Identity.Id, provider);
            await RefreshSnapshotAsync(true);
        });
    }

    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Disconnecting…", async () =>
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
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task RefreshProvidersAsync()
    {
        var selectedId = (ProviderComboBox.SelectedItem as ProviderProposal)?.ProviderId;
        var providers = await _backend.GetProvidersAsync();
        ProviderComboBox.ItemsSource = providers;
        ProviderComboBox.SelectedItem = providers.FirstOrDefault(p => p.ProviderId == selectedId) ?? providers.FirstOrDefault();
        AppendActivity($"Loaded {providers.Count} provider proposals.");
        UpdateControls();
    }

    private async Task RefreshSnapshotAsync(bool showErrors)
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            _snapshot = await _backend.GetSnapshotAsync();
            RenderSnapshot();
        }
        catch (Exception ex)
        {
            if (showErrors) ShowError(ex);
            FooterStatusText.Text = "Backend status is temporarily unavailable.";
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RenderSnapshot()
    {
        ConnectionStatusText.Text = _snapshot.IsConnected ? "Connected" : _snapshot.NodeUp ? "Not connected" : "Backend offline";
        ConnectionDetailText.Text = _snapshot.IsConnected
            ? "The provider proxy is ready. You can launch the isolated browser."
            : _snapshot.NodeUp
                ? "Open Controls in the upper-right, choose a provider, and connect."
                : "The native controller cannot reach the Myst backend.";
        IdentityText.Text = _snapshot.Identity is null
            ? "No identity exists yet. Create one to begin."
            : $"{ShortId(_snapshot.Identity.Id)}\nRegistration: {_snapshot.Identity.RegistrationStatus}";
        AcceptTermsCheckBox.IsChecked = _snapshot.Terms?.IsCurrent == true;
        TermsVersionText.Text = _snapshot.Terms is null
            ? "Version unavailable"
            : $"Current version: {_snapshot.Terms.CurrentVersion}";
        FooterStatusText.Text = _snapshot.NodeUp
            ? $"Native UI · Backend control 127.0.0.1:{BackendController.ControlPort} · Browser proxy 127.0.0.1:{BackendController.ProxyPort}"
            : "Native UI · Waiting for backend";
        UpdateControls();
    }

    private void UpdateControls()
    {
        var hasIdentity = _snapshot.Identity is not null;
        var termsAccepted = _snapshot.Terms?.IsCurrent == true;
        var isRegistered = _snapshot.Identity?.RegistrationStatus.Equals("Registered", StringComparison.OrdinalIgnoreCase) == true;
        var hasProvider = ProviderComboBox.SelectedItem is ProviderProposal;
        AcceptTermsCheckBox.IsEnabled = !_busy && _snapshot.NodeUp && !termsAccepted;
        CreateIdentityButton.IsEnabled = !_busy && _snapshot.NodeUp && termsAccepted && !hasIdentity;
        RegisterIdentityButton.IsEnabled = !_busy && _snapshot.NodeUp && termsAccepted && hasIdentity && !isRegistered;
        RefreshProvidersButton.IsEnabled = !_busy && _snapshot.NodeUp;
        ConnectButton.IsEnabled = !_busy && _snapshot.NodeUp && termsAccepted && hasIdentity && hasProvider && !_snapshot.IsConnected;
        DisconnectButton.IsEnabled = !_busy && _snapshot.IsConnected;
        DisconnectMainButton.IsEnabled = !_busy && _snapshot.IsConnected;
        LaunchBrowserButton.IsEnabled = !_busy && _snapshot.IsConnected;
    }

    private async Task RunBusyAsync(string status, Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        FooterStatusText.Text = status;
        UpdateControls();
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _busy = false;
            UpdateControls();
        }
    }

    private void AppendActivity(string message)
    {
        ActivityText.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        ActivityText.ScrollToEnd();
    }

    private void ShowError(Exception ex)
    {
        AppendActivity($"Error: {ex.Message}");
        MessageBox.Show(this, ex.Message, "Privacy Browser", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string ShortId(string id) => id.Length > 22 ? id[..22] + "…" : id;
}
