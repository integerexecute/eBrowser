using Microsoft.UI.Xaml;

namespace eBrowser.Windows
{
    public partial class App : Application
    {
        private MainWindow? _window;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }

        private void SetupTrayMenu()
        {
        }
    }
}