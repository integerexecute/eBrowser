using eBrowser.Views;
using Gtk;
using System;
using UI = Gtk.Builder.ObjectAttribute;

namespace eBrowser.Linux
{
    internal class MainWindow : Window
    {
        public PostsView postsView = new PostsView();

        public MainWindow() : this(new Builder("MainWindow.glade")) { }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);

            DeleteEvent += Window_DeleteEvent;
            Child = postsView;
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }
    }
}
