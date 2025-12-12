using e621NET;
using eBrowser.Windows.Views.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Linq;
using SystemTray.Core;

namespace eBrowser.Windows
{
    public sealed partial class MainWindow : Window
    {
        public static e621Client Client = new e621Client(new e621ClientOptions()
        { UserAgent = "eBrowser/1.0 (disotakyu)", });

        public static Page ActivePage = null!;
        public static event Action<object, KeyRoutedEventArgs>? OnKeyDown;
        public static Action<UIElement> RegisterKeyDown = null!;

        private SystemTrayManager systemTrayManager;
        private WindowHelper windowHelper;

        public MainWindow()
        {
            InitializeComponent();
            CustomizeTitleBar();

            Content.AddHandler(UIElement.KeyDownEvent,
                new KeyEventHandler(Content_PreviewKeyDown),
                handledEventsToo: true);
            RootFrame.Navigated += OnRootFrameNavigated;

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
            RootFrame.Navigate(typeof(PostsPage));
            RootFrame.Focus(FocusState.Programmatic);
        }

        private void Configuration_OnHideToSystemTrayChanged(bool obj)
        {
            systemTrayManager.CloseButtonMinimizesToTray = obj;
        }

        private void OnRootFrameNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is Page page)
            {
                Debug.WriteLine($"ActivePage set as: " + page.GetType().Name);
                ActivePage = page;
            }

            Type pageType = e.SourcePageType;
            foreach (NavigationViewItem item in NavView.MenuItems.Cast<NavigationViewItem>())
            {
                if (item.Tag?.ToString() == pageType.Name)
                {
                    NavView.SelectedItem = item;
                    return;
                }
            }

            if (pageType == typeof(SettingsPage))
            {
                NavView.SelectedItem = NavView.SettingsItem;
            }
        }

        private void Content_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            Debug.WriteLine($"Key: {e.Key}");
            if (ActivePage is IPageKeyHandler handler)
            {
                handler.OnPageKeyDown(sender, e);
            }
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

    public interface IPageKeyHandler
    {
        void OnPageKeyDown(object sender, KeyRoutedEventArgs e);
    }
}