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
using System.Text.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Core;

namespace eBrowser.Windows.Views.Pages
{
    public sealed partial class PostsPage : Page, IPageKeyHandler
    {
        private ItemsWrapGrid? wrapGrid;

        public ePosts? originalQuery;
        public ObservableCollection<ePost> ShownPosts { get; } = [];
        public string SortValue = "Date";
        public string postsPath => Path.Combine(Configuration.appDataDirectory, "posts.json");

        public PostsPage()
        {
            Debug.WriteLine("[PostsPage] Constructor called");
            if (MainWindow.ActivePage == null)
                MainWindow.ActivePage = this;

            InitializeComponent();

            BtnSearch.Click += BtnSearch_Click;
            ImageGridView.LayoutUpdated += ImageGridView_LayoutUpdated;
            SearchBox.KeyDown += SearchBox_KeyDown;

            if (File.Exists(postsPath))
            {
                var content = File.ReadAllText(postsPath);
                var data = JsonSerializer.Deserialize<ePosts>(content);
                if (data != null)
                {
                    currentPage = data.Page;
                    totalPages = data.MaxPage;
                    data.MaxPage = 750;

                    SearchBox.Text = data.Query;
                    ShowPosts(data, true);

                    SearchBox.IsEnabled = true;
                    BtnSearch.IsEnabled = true;
                    Focus(FocusState.Programmatic);
                }
            }
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

            Configuration.EnsureAppDataDirectoryExists();
            File.WriteAllText(postsPath, JsonSerializer.Serialize(posts));
            ShowPosts(posts, initialSearch);

            SearchBox.IsEnabled = true;
            BtnSearch.IsEnabled = true;
            Focus(FocusState.Programmatic);
        }

        public void ShowPosts(ePosts posts, bool initialSearch)
        {
            originalQuery = posts;

            ShownPosts.Clear();
            if (posts.Posts.Count > 0)
            {
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
            if (originalQuery == null) return;

            if (currentPage > 1)
            {
                currentPage--;
                SearchBox.Text = originalQuery.Query;
                SearchPost(false);
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (originalQuery == null) return;

            if (currentPage < totalPages)
            {
                currentPage++;
                SearchBox.Text = originalQuery.Query;
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

        public void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (SearchBox.FocusState != FocusState.Unfocused)
                return;

            // Check if the event was already handled
            if (e.Handled)
                return;

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
                    case VirtualKey.E:
                        Debug.WriteLine("[PostsPage] E → Focus SearchBox");
                        SearchBox.Focus(FocusState.Programmatic);
                        e.Handled = true;
                        break;

                    case VirtualKey.A:
                        Debug.WriteLine("[PostsPage] A → Back page");
                        BtnBack_Click(this, e);
                        e.Handled = true;
                        break;

                    case VirtualKey.D:
                        Debug.WriteLine("[PostsPage] D → Next page");
                        BtnNext_Click(this, e);
                        e.Handled = true;
                        break;

                    case VirtualKey.S:
                        Debug.WriteLine("[PostsPage] S → Open first post");
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
        }
    }
}
