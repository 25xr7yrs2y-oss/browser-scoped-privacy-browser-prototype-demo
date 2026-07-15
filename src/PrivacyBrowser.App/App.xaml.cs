using System.Windows;

namespace PrivacyBrowser.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var options = AppOptions.Parse(e.Args);
            var window = new MainWindow(options);
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            string? logPath = TryWriteStartupError(ex);
            string message = ex.GetBaseException().Message;
            if (logPath is not null)
            {
                message += $"{Environment.NewLine}{Environment.NewLine}Diagnostic details: {logPath}";
            }
            MessageBox.Show(message, "Privacy Browser", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static string? TryWriteStartupError(Exception exception)
    {
        try
        {
            string stateDirectory = Path.Combine(AppContext.BaseDirectory, "state");
            Directory.CreateDirectory(stateDirectory);
            string path = Path.Combine(stateDirectory, "startup-error.log");
            File.WriteAllText(path, $"{DateTimeOffset.Now:O}{Environment.NewLine}{exception}");
            return path;
        }
        catch
        {
            return null;
        }
    }
}
