using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace NeoSSH
{
    public class FontSelectorDialog : ContentDialog
    {
        public string SelectedFont { get; private set; }

        private TextBox _searchBox;
        private ListBox _listBox;
        private TextBlock _preview;
        private Slider _sizeSlider;
        private List<string> _fonts = new List<string>();

        public FontSelectorDialog()
        {
            this.Title = "Select Font";
            this.PrimaryButtonText = "OK";
            this.SecondaryButtonText = "Cancel";

            // Build root grid: search row, content row
            var root = new Grid { Padding = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            _searchBox = new TextBox { PlaceholderText = "Search fonts...", Height = 36, Margin = new Thickness(0,0,0,8) };
            _searchBox.TextChanged += (s, e) => FilterFonts(_searchBox.Text);
            Grid.SetRow(_searchBox, 0);
            root.Children.Add(_searchBox);

            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            Grid.SetRow(contentGrid, 1);

            _listBox = new ListBox { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            _listBox.SelectionChanged += (s, e) => UpdatePreview();
            _listBox.DoubleTapped += (s, e) =>
            {
                if (_listBox.SelectedItem is string fam)
                {
                    SelectedFont = fam;
                    try { this.Hide(); } catch { }
                }
            };
            // allow the ListBox to scroll
            _listBox.MaxHeight = 400;
            ScrollViewer.SetVerticalScrollBarVisibility(_listBox, ScrollBarVisibility.Auto);
            Grid.SetColumn(_listBox, 0);
            contentGrid.Children.Add(_listBox);

            var previewPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8, Margin = new Thickness(12,0,0,0) };
            _preview = new TextBlock { Text = "The quick brown fox jumps over the lazy dog 0123456789", TextWrapping = TextWrapping.Wrap, MinWidth = 260, MinHeight = 140 };
            _sizeSlider = new Slider { Minimum = 8, Maximum = 48, Value = 14, Width = 160 };
            _sizeSlider.ValueChanged += (s, e) => { _preview.FontSize = e.NewValue; };
            var sizePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            sizePanel.Children.Add(new TextBlock { Text = "Size:", VerticalAlignment = VerticalAlignment.Center });
            sizePanel.Children.Add(_sizeSlider);
            previewPanel.Children.Add(_preview);
            previewPanel.Children.Add(sizePanel);
            Grid.SetColumn(previewPanel, 1);
            contentGrid.Children.Add(previewPanel);

            root.Children.Add(contentGrid);

            // constrain overall dialog size via ContentDialog's Max/Min
            this.MaxWidth = 820; this.MaxHeight = 600; this.MinWidth = 600; this.MinHeight = 380;

            this.Content = root;

            this.Loaded += FontSelectorDialog_Loaded;
            this.PrimaryButtonClick += FontSelectorDialog_PrimaryButtonClick;
        }

        private void FontSelectorDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_listBox.SelectedItem is string fam)
            {
                SelectedFont = fam;
            }
        }

        private void FontSelectorDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadSystemFonts();
                _listBox.ItemsSource = _fonts;
                if (_fonts.Count > 0)
                {
                    _listBox.SelectedIndex = 0;
                }
            }
            catch
            {
            }
        }

        private void FilterFonts(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _listBox.ItemsSource = _fonts;
                return;
            }
            var lowered = text.ToLowerInvariant();
            var filtered = _fonts.Where(f => f.ToLowerInvariant().Contains(lowered)).ToList();
            _listBox.ItemsSource = filtered;
        }

        private void UpdatePreview()
        {
            if (_listBox.SelectedItem is string fam)
            {
                try
                {
                    _preview.FontFamily = new FontFamily(fam);
                }
                catch { }
            }
        }

        private void LoadSystemFonts()
        {
            // Read installed fonts from registry
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Fonts"))
                {
                    if (key == null) return;
                    var names = key.GetValueNames();
                    var list = new List<string>();
                    foreach (var name in names)
                    {
                        // value names often like "Arial (TrueType)" or "Segoe UI (TrueType)"
                        var fam = name;
                        // remove parts after '\\0' if any
                        var idx = fam.IndexOf('\0');
                        if (idx > 0) fam = fam.Substring(0, idx);
                        // remove parenthetical suffix
                        var paren = fam.IndexOf('(');
                        if (paren > 0) fam = fam.Substring(0, paren);
                        fam = fam.Trim();
                        if (!string.IsNullOrEmpty(fam) && !list.Contains(fam)) list.Add(fam);
                    }
                    list.Sort(StringComparer.InvariantCultureIgnoreCase);
                    _fonts = list;
                }
            }
            catch
            {
                // fallback: a small set of common fonts
                _fonts = new List<string> { "Segoe UI", "Arial", "Calibri", "Times New Roman", "Consolas", "Courier New" };
            }
        }
    }
}
