using System.Windows;

namespace ASidekick
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            MainWindow mainWindow = new MainWindow();
            if (!mainWindow.HasSecondaryDisplay)
            {
                System.Windows.MessageBox.Show(
                    "No secondary display was detected. Connect and enable the display in Windows Settings > System > Display, then try again.",
                    "ASidekick",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            mainWindow.Show();
        }
    }
}
