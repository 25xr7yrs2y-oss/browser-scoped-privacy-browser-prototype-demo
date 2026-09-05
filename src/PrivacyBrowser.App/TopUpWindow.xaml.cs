using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace PrivacyBrowser.App;

public partial class TopUpWindow : Window
{
    private readonly BackendController _backend;
    private readonly string _identityId;
    private bool _busy;
    private Uri? _paymentUri;

    public TopUpWindow(BackendController backend, string identityId)
    {
        InitializeComponent();
        _backend = backend;
        _identityId = identityId;
        try
        {
            CountryTextBox.Text = RegionInfo.CurrentRegion.TwoLetterISORegionName;
        }
        catch (ArgumentException)
        {
            CountryTextBox.Text = "US";
        }
        Loaded += TopUpWindow_Loaded;
    }

    public PaymentOrder? CreatedOrder { get; private set; }

    private async void TopUpWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            var gateways = await _backend.GetPaymentGatewaysAsync();
            GatewayComboBox.ItemsSource = gateways;
            GatewayComboBox.SelectedItem = gateways.FirstOrDefault();
            if (gateways.Count == 0)
            {
                throw new InvalidOperationException("No payment gateways are currently available from Mysterium.");
            }
        });
    }

    private void GatewayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GatewayComboBox.SelectedItem is not PaymentGateway gateway) return;
        CurrencyComboBox.ItemsSource = gateway.Currencies;
        CurrencyComboBox.SelectedItem = gateway.Currencies.FirstOrDefault();
        GatewayHelpText.Text = gateway.OrderOptions.Minimum > 0
            ? $"Minimum: more than {gateway.OrderOptions.Minimum:0.####} MYST"
            : "This gateway did not report a minimum amount.";
        var suggested = gateway.OrderOptions.Suggested.FirstOrDefault(v => v > gateway.OrderOptions.Minimum);
        if (suggested > 0) AmountTextBox.Text = suggested.ToString(CultureInfo.InvariantCulture);
    }

    private async void CreateOrderButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorBorder.Visibility = Visibility.Collapsed;
        if (GatewayComboBox.SelectedItem is not PaymentGateway gateway)
        {
            ShowError("Choose a payment gateway.");
            return;
        }
        if (!decimal.TryParse(AmountTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
        {
            ShowError("Enter the MYST amount using digits and a decimal point.");
            return;
        }
        if (CurrencyComboBox.SelectedItem is not string currency)
        {
            ShowError("Choose a payment currency.");
            return;
        }

        await RunAsync(async () =>
        {
            _paymentUri = null;
            CreatedOrder = null;
            OpenPaymentButton.Visibility = Visibility.Collapsed;
            ResultBorder.Visibility = Visibility.Collapsed;

            var order = await _backend.CreatePaymentOrderAsync(
                _identityId, gateway, amount, currency, CountryTextBox.Text.Trim(), StateTextBox.Text.Trim());
            var paymentTarget = order.GetPaymentTarget(gateway.Name);
            CreatedOrder = order;
            _paymentUri = paymentTarget.PaymentUri;
            ResultTitleText.Text = "Payment order created";
            ResultDetailText.Text = $"Order {CreatedOrder.Id}\nStatus: {CreatedOrder.Status}\n" +
                $"Receive: {CreatedOrder.ReceiveMyst} MYST\nPay: {CreatedOrder.PayAmount} {CreatedOrder.PayCurrency}";
            ResultBorder.Visibility = Visibility.Visible;
            OpenPaymentButton.Visibility = _paymentUri is null ? Visibility.Collapsed : Visibility.Visible;
            CreateOrderButton.Content = "Create another order";
        });
    }

    private void OpenPaymentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_paymentUri is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(_paymentUri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError(BackendErrorTranslator.ToUserMessage(ex));
        }
    }

    private void CopyPaymentButton_Click(object sender, RoutedEventArgs e)
    {
        if (CreatedOrder is null) return;
        Clipboard.SetText($"Mysterium payment order: {CreatedOrder.Id}\nStatus: {CreatedOrder.Status}\n" +
            $"Receive: {CreatedOrder.ReceiveMyst} MYST\nPay: {CreatedOrder.PayAmount} {CreatedOrder.PayCurrency}");
        CopyPaymentButton.Content = "Copied";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        LoadingProgress.Visibility = Visibility.Visible;
        CreateOrderButton.IsEnabled = false;
        GatewayComboBox.IsEnabled = false;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowError(BackendErrorTranslator.ToUserMessage(ex));
        }
        finally
        {
            _busy = false;
            LoadingProgress.Visibility = Visibility.Collapsed;
            CreateOrderButton.IsEnabled = true;
            GatewayComboBox.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBorder.Visibility = Visibility.Visible;
    }
}
