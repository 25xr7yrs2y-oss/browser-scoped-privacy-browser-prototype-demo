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
            MessageBox.Show(ex.Message, "Privacy Browser", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
