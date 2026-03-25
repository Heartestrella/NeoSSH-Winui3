using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using NeoSSH.Services;
using JVK = System.Text.Json.JsonValueKind;
namespace NeoSSH
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Close pane on load for compact start
            NavView.Loaded += (_, __) => NavView.IsPaneOpen = false;
            NavView.SelectedItem = HomeNavItem;

            // Hides the default system title bar.
            ExtendsContentIntoTitleBar = true;
            // Replace system title bar with the WinUI TitleBar control.
            SetTitleBar(TitleBar);

            // Navigate to home page by default
            ContentFrame.Navigate(typeof(HomePage));

            // Restore background and opacity from config after window is fully loaded
            this.Activated += MainWindow_FirstActivated;
        }

        private bool _backgroundRestored = false;
        private void MainWindow_FirstActivated(object sender, WindowActivatedEventArgs args)
        {
            if (_backgroundRestored) return;
            _backgroundRestored = true;
            this.Activated -= MainWindow_FirstActivated;

            try
            {
                int pct = ConfigManager.Instance.Get<int>("background_opacity", 35);
                var opacity = Math.Max(0.0, Math.Min(1.0, pct / 100.0));
                BackgroundImage.Opacity = opacity;

                var bgPic = ConfigManager.Instance.Get<string>("bg_pic", "");
                Console.WriteLine(bgPic);
                if (!string.IsNullOrEmpty(bgPic) && File.Exists(bgPic))
                {
                    var uri = new Uri(bgPic, UriKind.Absolute);
                    var img = new BitmapImage(uri);
                    BackgroundImage.Source = img;
                }
            }
            catch { }

            // Restore font settings from config
            try
            {
                var fontFamily = ConfigManager.Instance.Get<string>("editor_font", "");
                if (!string.IsNullOrEmpty(fontFamily))
                    SetGlobalFont(fontFamily);

                var fontSizeStr = ConfigManager.Instance.Get<string>("font_size", "12");
                if (double.TryParse(fontSizeStr, out double fontSize) && fontSize > 0)
                    SetGlobalFontSize(fontSize);
            }
            catch { }
        }

        /// <summary>
        /// Set global background image by file path. Called by BackgroundService.
        /// </summary>
        public void SetGlobalBackground(string path)
        {
            try
            {
                var uri = new Uri(path);
                var img = new BitmapImage(uri);
                // Ensure UI thread
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    BackgroundImage.Source = img;
                });
            }
            catch
            {
                // ignore invalid path
            }
        }

        public void SetGlobalBackgroundOpacity(double opacity)
        {
            var clamped = Math.Max(0.0, Math.Min(1.0, opacity));
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (BackgroundImage != null)
                    BackgroundImage.Opacity = clamped;
            });
        }

        public double GetGlobalBackgroundOpacity()
        {
            try
            {
                return BackgroundImage?.Opacity ?? 1.0;
            }
            catch
            {
                return 1.0;
            }
        }

        /// <summary>
        /// Set the global font family on the ContentFrame so all pages inherit it.
        /// </summary>
        public void SetGlobalFont(string fontFamily)
        {
            if (string.IsNullOrEmpty(fontFamily)) return;
            this.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    ContentFrame.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(fontFamily);
                }
                catch { }
            });
        }

        /// <summary>
        /// Set the global font size on the ContentFrame so all pages inherit it.
        /// </summary>
        public void SetGlobalFontSize(double size)
        {
            if (size <= 0) return;
            this.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    ContentFrame.FontSize = size;
                }
                catch { }
            });
        }

        private void NavView_SelectionChanged(NavigationView sender,
                                           NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
                return;
            }

            // 处理普通菜单项
            var selectedItem = args.SelectedItem as NavigationViewItem;
            if (selectedItem == null) return;

            string tag = selectedItem.Tag?.ToString();
            switch (tag)
            {
                case "home_page":
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
                //case "ssh_page":
                //    ContentFrame.Navigate(typeof(SSHPage));
                //    break;
            }
        }
    }
}
