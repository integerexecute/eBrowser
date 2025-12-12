using e621NET.Data.Posts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;

namespace eBrowser.Windows.Views.Pages
{
    public sealed partial class ViewerPage : Page, IPageKeyHandler
    {
        private List<ePost> currentPosts = [];
        private ePost? currentPost;

        public ViewerPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is ViewerParams p)
            {
                currentPosts = p.Posts;
                currentPost = p.Post;
                Load();
            }
        }

        /// <summary>
        /// Loads the current post and show it
        /// </summary>
        public void Load()
        {
            if (currentPosts == null || currentPost == null) return;
            ShowPost(currentPosts.IndexOf(currentPost));
        }

        private async void ShowPost(int index)
        {
            if (currentPosts == null) return;
            if (index < 0 || index >= currentPosts.Count) return;

            currentPost = currentPosts[index];

            // Update UI
            IndexLabel.Text = $"{index + 1} / {currentPosts.Count}";
            DimensionLabel.Text = $"Dimension: {currentPost.File.Width} x {currentPost.File.Height}";
            ExtensionLabel.Text = $"Extension: {currentPost.File.Ext}";
            FileSizeLabel.Text = $"File Size: {FormatBytes(currentPost.File.Size)}";
            IdLabel.Text = $"Id: {currentPost.Id}";

            // Populate lists
            Artists.ItemsSource = currentPost.Tags.Artist;
            Characters.ItemsSource = currentPost.Tags.Character.Concat(currentPost.Tags.Species);
            Tags.ItemsSource = currentPost.Tags.General
                                            .Concat(currentPost.Tags.Copyright)
                                            .Concat(currentPost.Tags.Lore)
                                            .Concat(currentPost.Tags.Meta);
            Sources.ItemsSource = currentPost.Sources;
            Focus(FocusState.Programmatic);

            // Load into WebView2
            await LoadMediaToWebViewAsync();
        }

        private async Task LoadMediaToWebViewAsync()
        {
            if (currentPost == null) return;
            await webView.EnsureCoreWebView2Async();

            string ext = currentPost.File.Ext?.ToLower() ?? "png";
            bool isVideo = ext is "mp4" or "webm" or "m4a";

            string url = currentPost.File.Url ?? "";

            string basePath = AppContext.BaseDirectory;
            string htmlPath = Path.Combine(basePath, "Assets", "HTML",
                isVideo ? "VideoView.html" : "ImageView.html");

            string html = await File.ReadAllTextAsync(htmlPath);
            html = html.Replace(isVideo ? "{VIDEO_URL}" : "{IMAGE_URL}", url);
            if (isVideo)
                html = html.Replace("{ADDITIONAL_PARAM}", Configuration.Current.AutoMuteVideo ? " muted" : "");

            webView.NavigateToString(html);

            webView.CoreWebView2.DOMContentLoaded += async (_, _) =>
            {
                if (isVideo && Configuration.Current.AutoPlayVideo)
                {
                    await webView.CoreWebView2.ExecuteScriptAsync("playVideo();");
                }
            };
            Focus(FocusState.Programmatic);
        }

        public static string FormatBytes(long bytes)
        {
            string[] sizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

            int i = 0;
            double dblSByte = bytes;

            if (bytes > 1024)
            {
                for (i = 0; (bytes / 1024) > 0; i++, bytes /= 1024)
                {
                    dblSByte = bytes / 1024.0;
                }
            }

            return string.Format("{0:0.##} {1}", dblSByte, sizeSuffixes[i]);
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (currentPosts == null || currentPost == null) return;

            int index = currentPosts.IndexOf(currentPost);
            index = index > 0 ? index - 1 : currentPosts.Count - 1;
            ShowPost(index);
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (currentPosts == null || currentPost == null) return;

            int index = currentPosts.IndexOf(currentPost);
            index = (index + 1) % currentPosts.Count;
            ShowPost(index);
        }

        public void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (MainWindow.ActivePage is not ViewerPage) return;
            if (e.Handled)
            {
                Debug.WriteLine("[ViewerPage] Handled key detected");
                return;
            }

            switch (e.Key)
            {
                case VirtualKey.A:
                    Debug.WriteLine("[ViewerPage] Back post");
                    BtnBack_Click(sender, e);
                    e.Handled = true;
                    break;

                case VirtualKey.D:
                    Debug.WriteLine("[ViewerPage] Next post");
                    BtnNext_Click(sender, e);
                    e.Handled = true;
                    break;

                case VirtualKey.Escape:
                    Debug.WriteLine("[ViewerPage] Go back");
                    if (Frame.CanGoBack) Frame.GoBack();
                    e.Handled = true;
                    break;
            }
        }
    }

    public class ViewerParams
    {
        public List<ePost> Posts { get; set; } = [];
        public ePost Post { get; set; }

        public ViewerParams(List<ePost> posts, ePost post)
        {
            Posts = posts;
            Post = post;
        }
    }
}
