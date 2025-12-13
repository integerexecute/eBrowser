using e621NET;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace eBrowser.Windows.Views.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public Configuration Config => Configuration.Current;

        public SettingsPage()
        {
            DataContext = Config;
            Debug.WriteLine("[SettingsPage] Constructor called");
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Config.Username) && !string.IsNullOrWhiteSpace(Config.ApiKey))
                MainWindow.Client.SetCredentials(new e621APICredentials(Config.Username, Config.ApiKey));
            Configuration.Save();
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            Configuration.OnHideToSystemTrayChanged?.Invoke(HideToSysTray.IsOn);
        }
    }
}
