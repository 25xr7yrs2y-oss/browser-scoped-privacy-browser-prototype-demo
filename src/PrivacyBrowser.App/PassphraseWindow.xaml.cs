using System.Windows;

namespace PrivacyBrowser.App;

public partial class PassphraseWindow : Window
{
    private readonly int _minimumLength;
    private readonly bool _requireConfirmation;

    public PassphraseWindow(
        string heading,
        string description,
        string actionLabel,
        bool requireConfirmation,
        int minimumLength)
    {
        InitializeComponent();
        HeadingText.Text = heading;
        DescriptionText.Text = description;
        ContinueButton.Content = actionLabel;
        _requireConfirmation = requireConfirmation;
        _minimumLength = minimumLength;
        ConfirmationPanel.Visibility = requireConfirmation ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) => PassphraseBox.Focus();
    }

    /// <summary>
    /// Returns the credential once and immediately clears both PasswordBox controls.
    /// The caller must keep the returned string local and must never persist or log it.
    /// </summary>
    public string TakePassphrase()
    {
        var passphrase = PassphraseBox.Password;
        PassphraseBox.Clear();
        ConfirmationBox.Clear();
        return passphrase;
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        if (PassphraseBox.Password.Length < _minimumLength)
        {
            ValidationText.Text = _minimumLength <= 1
                ? "Enter the identity passphrase."
                : $"Use at least {_minimumLength} characters for a new identity passphrase.";
            return;
        }
        if (_requireConfirmation && PassphraseBox.Password != ConfirmationBox.Password)
        {
            ValidationText.Text = "The passphrases do not match.";
            ConfirmationBox.Clear();
            ConfirmationBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        PassphraseBox.Clear();
        ConfirmationBox.Clear();
        DialogResult = false;
    }
}
