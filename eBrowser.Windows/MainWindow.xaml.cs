using e621NET;
using eBrowser.Windows.Views.Pages;
using H.NotifyIcon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using SystemTray.Core;
using SystemTray.UI;
using static SystemTray.Core.SystemTrayManager;

namespace eBrowser.Windows
{
    public sealed partial class MainWindow : Window
    {
        public static e621Client Client = new e621Client(new e621ClientOptions()
        { UserAgent = "eBrowser/1.0 (disotakyu)", });

        public static Page ActivePage = null!;
        public static event Action<object, KeyRoutedEventArgs>? OnKeyDown;

        private SystemTrayManager systemTrayManager;
        private WindowHelper windowHelper;

        public MainWindow()
        {
            InitializeComponent();
            CustomizeTitleBar();

            NavView.PreviewKeyDown += RootFrame_PreviewKeyDown;
            RootFrame.PreviewKeyDown += RootFrame_PreviewKeyDown;
            RootFrame.Navigated += OnRootFrameNavigated;
            AppWindow.Closing += (s, e) =>
            {
                if (Configuration.Current.HideToSystemTray)
                {
                    e.Cancel = true;
                    this.Hide();
                }
            };

            windowHelper = new WindowHelper(this);
            systemTrayManager = new SystemTrayManager(windowHelper)
            {
                IconToolTip = "eBrowser",
                MinimizeToTray = false,
                CloseButtonMinimizesToTray = Configuration.Current.HideToSystemTray
            };

            Configuration.OnHideToSystemTrayChanged += Configuration_OnHideToSystemTrayChanged; 

            systemTrayManager.CloseButtonMinimizesToTray = true;
            systemTrayManager.OpenSettingsAction = () =>
            {
                RootFrame.Navigate(typeof(SettingsPage));
            };

            NavView.SelectedItem = NavView.MenuItems[0];
            RootFrame.Navigate(typeof(Views.Pages.PostsPage));
            RootFrame.Focus(FocusState.Programmatic);
        }

        private void Configuration_OnHideToSystemTrayChanged(bool obj)
        {
            systemTrayManager.CloseButtonMinimizesToTray = obj;
        }

        private void OnRootFrameNavigated(object sender, NavigationEventArgs e)
        {
            // Update ActivePage reference
            if (e.Content is Page page)
            {
                Debug.WriteLine($"ActivePage set as: " + page.GetType().Name);
                ActivePage = page;
            }

            // Your existing navigation view sync logic
            Type pageType = e.SourcePageType;

            foreach (NavigationViewItem item in NavView.MenuItems)
            {
                if (item.Tag?.ToString() == pageType.Name)
                {
                    NavView.SelectedItem = item;
                    return;
                }
            }

            if (pageType == typeof(Views.Pages.SettingsPage))
            {
                NavView.SelectedItem = NavView.SettingsItem;
            }
        }

        private void RootFrame_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            OnKeyDown?.Invoke(sender, e);
        }

        private void NavView_SelectionChanged(
            NavigationView sender,
            NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                RootFrame.Navigate(typeof(SettingsPage));
                return;
            }

            string? tag = ((NavigationViewItem)args.SelectedItem)!.Tag.ToString();

            switch (tag)
            {
                case "PostsPage":
                    RootFrame.Navigate(typeof(Views.Pages.PostsPage));
                    break;

                case "ViewerPage":
                    RootFrame.Navigate(typeof(Views.Pages.ViewerPage));
                    break;
            }
        }

        private void CustomizeTitleBar()
        {
            ExtendsContentIntoTitleBar = true;
            // Check to see if customization is supported.
            // The method returns true on Windows 10 since Windows App SDK 1.2,
            // and on all versions of Windows App SDK on Windows 11.
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                AppWindowTitleBar m_TitleBar = AppWindow.TitleBar;
                Title = "eBrowser";
            }
        }
    }
}