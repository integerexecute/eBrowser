using e621NET;
using e621NET.Data.Posts;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Core;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace eBrowser.Windows.Views.Pages
{
    public sealed partial class PostsPage : Page
    {
        private ItemsWrapGrid? wrapGrid;

        public ePosts? originalQuery;
        public ObservableCollection<ePost> ShownPosts { get; } = [];
        public string SortValue = "Date";

        public PostsPage()
        {
            Debug.WriteLine("[PostsPage] Constructor called");
            if (MainWindow.ActivePage == null)
                MainWindow.ActivePage = this;

            InitializeComponent();
            MainWindow.OnKeyDown += Page_PreviewKeyDown;

            BtnSearch.Click += BtnSearch_Click;
            ImageGridView.LayoutUpdated += ImageGridView_LayoutUpdated;
            SearchBox.KeyDown += SearchBox_KeyDown;
        }

        private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                BtnSearch_Click(sender, e);
                e.Handled = true;
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            currentPage = 1;
            SearchPost(true);
        }

        public async void SearchPost(bool initialSearch)
        {
            BtnSearch.IsEnabled = false;
            SearchBox.IsEnabled = false;

            var posts = await MainWindow.Client.GetPostsAsync(SearchBox.Text, currentPage, fetchMaxPage: initialSearch);
            if (posts == null)
            {
                SearchBox.IsEnabled = true;
                BtnSearch.IsEnabled = true;
                return;
            }

            originalQuery = posts;

            if (posts.Posts.Count > 0)
            {
                ShownPosts.Clear();
                foreach (var post in posts.Posts)
                {
                    if (post.Preview.Url != null)
                        ShownPosts.Add(post);
                }

                currentPage = posts.Page;
                if (initialSearch)
                    totalPages = posts.MaxPage;

                if (!SearchHistoryList.Items.Contains(SearchBox.Text))
                    SearchHistoryList.Items.Add(SearchBox.Text);

                UpdatePageLabel();
                ApplySort(SortValue);
            }

            SearchBox.IsEnabled = true;
            BtnSearch.IsEnabled = true;
            Focus(FocusState.Programmatic);
        }

        private void ImageGridView_LayoutUpdated(object? sender, object e) => UpdateItemWidth();
        private void ColumnCountBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => UpdateItemWidth();

        private void ImageGridView_Loaded(object sender, RoutedEventArgs e)
        {
            wrapGrid = ImageGridView.ItemsPanelRoot as ItemsWrapGrid;
            UpdateItemWidth();
        }

        private void UpdateItemWidth()
        {
            if (wrapGrid == null)
                return;

            double columns = ColumnCountBox.Value;
            if (columns < 1) columns = 1;

            double totalWidth = ImageGridView.ActualWidth;
            if (totalWidth <= 0)
                return;

            double itemWidth = totalWidth / columns;

            wrapGrid.ItemWidth = itemWidth;
            wrapGrid.ItemHeight = itemWidth;
        }

        private int currentPage = 1;
        private int totalPages = 10;

        private void UpdatePageLabel()
        {
            PageLabel.Text = $"{currentPage} / {totalPages}";
            BtnBack.IsEnabled = currentPage > 1;
            BtnNext.IsEnabled = currentPage < totalPages;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                currentPage--;
                SearchPost(false);
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage < totalPages)
            {
                currentPage++;
                SearchPost(false);
            }
        }

        private void ImageGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ePost post)
            {
                var param = new ViewerParams([.. ShownPosts], post);
                Frame.Navigate(typeof(ViewerPage), param);
            }
        }

        private void Page_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            Debug.WriteLine("==== [PostsPage] Key Event START ====");
            Debug.WriteLine($"Key: {e.Key}");
            Debug.WriteLine($"Handled before processing: {e.Handled}");

            // Check if this page is active
            if (MainWindow.ActivePage is not PostsPage)
            {
                Debug.WriteLine("Blocked: ActivePage is NOT PostsPage");
                Debug.WriteLine($"ActivePage is: {MainWindow.ActivePage?.GetType().Name ?? "null"}");
                Debug.WriteLine("==== [PostsPage] Key Event END ====");
                return;
            }

            Debug.WriteLine("Condition passed: ActivePage is PostsPage");

            // Check focus state
            Debug.WriteLine($"SearchBox focus state: {SearchBox.FocusState}");
            if (SearchBox.FocusState != FocusState.Unfocused)
            {
                Debug.WriteLine("Blocked: SearchBox IS focused");
                Debug.WriteLine("==== [PostsPage] Key Event END ====");
                return;
            }

            // Check if the event was already handled
            if (e.Handled)
            {
                Debug.WriteLine("Blocked: Event was already handled by another control");
                Debug.WriteLine("==== [PostsPage] Key Event END ====");
                return;
            }

            Debug.WriteLine("All conditions passed — processing key input");

            var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                .HasFlag(CoreVirtualKeyStates.Down);

            if (ctrl && e.Key == VirtualKey.L)
            {
                Debug.WriteLine("CTRL + L detected!");
                SearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }
            else
            {
                switch (e.Key)
                {
                    case VirtualKey.Left:
                        Debug.WriteLine("[PostsPage] LEFT ARROW → Back page");
                        BtnBack_Click(this, e);
                        e.Handled = true;
                        break;

                    case VirtualKey.Right:
                        Debug.WriteLine("[PostsPage] RIGHT ARROW → Next page");
                        BtnNext_Click(this, e);
                        e.Handled = true;
                        break;

                    case VirtualKey.Enter:
                        Debug.WriteLine("[PostsPage] ENTER → Open first post");
                        if (ShownPosts.Count > 0)
                        {
                            var param = new ViewerParams([.. ShownPosts], ShownPosts[0]);
                            Frame.Navigate(typeof(ViewerPage), param);
                        }
                        e.Handled = true;
                        break;

                    default:
                        Debug.WriteLine("Key not handled");
                        break;
                }
            }

            Debug.WriteLine($"Handled after processing: {e.Handled}");
            Debug.WriteLine("==== [PostsPage] Key Event END ====");
        }

        private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = SortBox.SelectedItem as ComboBoxItem;
            SortValue = item?.Content.ToString() ?? "";

            Debug.WriteLine($"Sort changed to: {SortValue}");
            ApplySort(SortValue);
        }

        private void ApplySort(string value)
        {
            if (ShownPosts.Count == 0)
                return;

            IEnumerable<ePost> sorted;

            switch (value)
            {
                case "Date":
                    sorted = ShownPosts.OrderByDescending(p => p.CreatedAt);
                    break;

                case "Favorites":
                    sorted = ShownPosts.OrderByDescending(p => p.FavCount);
                    break;

                case "Score":
                    sorted = ShownPosts.OrderByDescending(p => p.Score.Total);
                    break;

                default:
                    return;
            }

            // Replace items in the observable collection
            var sortedList = sorted.ToList();
            ShownPosts.Clear();
            foreach (var post in sortedList)
                ShownPosts.Add(post);

            Debug.WriteLine($"[PostsPage] Sorted by {value}");
        }
    }
}
