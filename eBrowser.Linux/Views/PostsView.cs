using Gtk;
using System;
using UI = Gtk.Builder.ObjectAttribute;

namespace eBrowser.Views
{
    public class PostsView : Bin
    {
        [UI] private SearchEntry postsSearchBox = null;
        [UI] private Button postsSearchBtn = null;

        private int _counter;

        public PostsView() : this(new Builder("PostsView.glade")) { }

        private PostsView(Builder builder) : base(builder.GetRawOwnedObject("PostsView"))
        {
            builder.Autoconnect(this);
            postsSearchBtn.Clicked += SearchBtn_Clicked;
        }

        private void SearchBtn_Clicked(object sender, EventArgs a)
        {
            _counter++;
            postsSearchBox.Text = $"So yeah: {_counter}";
        }
    }
}