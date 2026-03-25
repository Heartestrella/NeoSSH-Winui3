using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using NeoSSH.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;

namespace NeoSSH
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
            this.Loaded += Setting_page_Loaded;
        }

        private async void OnPickBackground(object sender, RoutedEventArgs e)
        {
            await OnPickBackgroundAsync();
        }

        private void Setting_page_Loaded(object sender, RoutedEventArgs e)
        {
            // Sync theme toggle if exists
            if (this.XamlRoot?.Content is FrameworkElement rootElement)
            {
                var themeToggle = this.FindName("ThemeToggle") as ToggleSwitch;
                if (themeToggle != null) themeToggle.IsOn = rootElement.ActualTheme == ElementTheme.Dark;
            }

            // Initialize controls from config
            try
            {
                // Language
                var lang = ConfigManager.Instance.Get<string>("language", "system");
                switch (lang?.ToLower())
                {
                    case "system": LanguageComboBox.SelectedIndex = 0; break;
                    case "english": LanguageComboBox.SelectedIndex = 1; break;
                    case "简体中文":
                    case "chinese": LanguageComboBox.SelectedIndex = 2; break;
                    case "日本語": LanguageComboBox.SelectedIndex = 3; break;
                    case "русский": LanguageComboBox.SelectedIndex = 4; break;
                    default: LanguageComboBox.SelectedIndex = 0; break;
                }

                // Terminal mode
                //var tmode = ConfigManager.Instance.Get<int>("terminal_mode", 0);
                //TerminalModeComboBox.SelectedIndex = Math.Min(Math.Max(0, tmode), TerminalModeComboBox.Items.Count - 1);

                // Update channel
                var upd = ConfigManager.Instance.Get<string>("update_channel", "none");
                if (!string.IsNullOrEmpty(upd))
                {
                    if (upd == "none") UpdateChannelComboBox.SelectedIndex = 0;
                    else if (upd == "stable") UpdateChannelComboBox.SelectedIndex = 1;
                    else UpdateChannelComboBox.SelectedIndex = 2;
                }

                // Right panel AI chat
                RightPanelAIChatToggle.IsOn = ConfigManager.Instance.Get<bool>("right_panel_ai_chat", true);

                // Background color
                var bgc = ConfigManager.Instance.Get<string>("bg_color", "Dark");
                bgc = (bgc ?? "").ToLower();
                if (bgc.Contains("light")) BackgroundColorComboBox.SelectedIndex = 0;
                else if (bgc.Contains("dark")) BackgroundColorComboBox.SelectedIndex = 1;
                else BackgroundColorComboBox.SelectedIndex = 2;

                // Background image opacity
                int pct = ConfigManager.Instance.Get<int>("background_opacity", 35);
                OpacitySlider.Value = Math.Max(0, Math.Min(100, pct));
                OpacityValueText.Text = $"{OpacitySlider.Value:0.##}%";

                // Lock ratio and follow CD
                LockRatioToggle.IsOn = ConfigManager.Instance.Get<bool>("locked_ratio", true);
                FollowCDToggle.IsOn = ConfigManager.Instance.Get<bool>("follow_cd", false);

                // Font sizes 12-30
                FontSizeComboBox.Items.Clear();
                for (int s = 12; s <= 30; s++) FontSizeComboBox.Items.Add(new ComboBoxItem { Content = s.ToString() });
                var fsize = ConfigManager.Instance.Get<string>("font_size", "12");
                for (int i = 0; i < FontSizeComboBox.Items.Count; i++)
                {
                    if (((ComboBoxItem)FontSizeComboBox.Items[i]).Content.ToString() == fsize)
                    {
                        FontSizeComboBox.SelectedIndex = i; break;
                    }
                }

                // Default view
                var dview = ConfigManager.Instance.Get<string>("default_view", "icon");
                DefaultViewComboBox.SelectedIndex = dview == "details" || dview == "detail" ? 1 : 0;

                // Single click
                SingleClickToggle.IsOn = ConfigManager.Instance.Get<bool>("file_tree_single_click", false);

                // Animation selection: try to match stored string
                var anim = ConfigManager.Instance.Get<string>("page_animation", "slide_fade");
                for (int i = 0; i < AnimationComboBox.Items.Count; i++)
                {
                    var item = (ComboBoxItem)AnimationComboBox.Items[i];
                    if (item.Content.ToString().Contains(anim.Replace('_', ' '), StringComparison.OrdinalIgnoreCase))
                    {
                        AnimationComboBox.SelectedIndex = i; break;
                    }
                }

                // Max concurrent transfers
                var maxc = ConfigManager.Instance.Get<int>("max_concurrent_transfers", 10);
                MaxConcurrentTransfersBox.Value = Math.Max(1, Math.Min(50000, maxc));

                // External editor
                ExternalEditorTextBox.Text = ConfigManager.Instance.Get<string>("external_editor", "");

                // Current font name - show on button
                var currentFont = ConfigManager.Instance.Get<string>("editor_font", "");
                if (!string.IsNullOrEmpty(currentFont))
                    SelectFontButton.Content = currentFont;

                // Font color - restore from config
                var savedColor = ConfigManager.Instance.Get<string>("font_color", "#FFFFFFFF");
                var restoredColor = ParseHexColor(savedColor);
                UpdateFontColorPreview(restoredColor);
            }
            catch { }
        }

        private void ThemeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            if (toggle != null && this.XamlRoot?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = toggle.IsOn ? ElementTheme.Dark : ElementTheme.Light;
            }
        }

        private async void Pick_img(object sender, RoutedEventArgs e)
        {
            // legacy method - delegate to OnPickBackground
            await OnPickBackgroundAsync();
        }
        private async System.Threading.Tasks.Task OnPickBackgroundAsync()
        {
            var picker = new FileOpenPicker();
            if (App.CurrentWindow == null) return;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                BackgroundService.SetBackgroundPath(file.Path);
            }
        }

        private void OnOpacitySliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            var slider = sender as Slider;
            if (slider == null) return;
            var value = slider.Value;
            OpacityValueText.Text = $"{value:0.##}%";
            BackgroundService.SetBackgroundOpacity(value / 100.0);
            // persist handled by BackgroundService
        }

        // --- Empty handlers required by XAML generated file (placeholders) ---
        private void OnSearchTextChanged(object sender, Microsoft.UI.Xaml.Controls.TextChangedEventArgs e) { }
        private void OnExternalEditorTextChanged(object sender, Microsoft.UI.Xaml.Controls.TextChangedEventArgs e)
        {
            ConfigManager.Instance.Set("external_editor", ((TextBox)sender).Text);
        }
        private void OnMaxConcurrentTransfersChanged(object sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs e)
        {
            ConfigManager.Instance.Set("max_concurrent_transfers", (int)e.NewValue);
        }
        private void OnUnbelievableButtonClick(object sender, RoutedEventArgs e) { }

        // --- Font color picker ---
        private bool _suppressColorChanged = false;

        private void OnFontColorChanged(Color color)
        {
            if (_suppressColorChanged) return;
            UpdateFontColorPreview(color);
            var hex = ColorToHex(color);
            ConfigManager.Instance.Set("font_color", hex);
        }

        private void UpdateFontColorPreview(Color color)
        {
            FontColorPreview.Background = new SolidColorBrush(color);
            FontColorHexText.Text = ColorToHex(color);
        }

        private static string ColorToHex(Color c)
            => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

        private static Color ParseHexColor(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                    hex = "FF" + hex;
                if (hex.Length == 8)
                {
                    byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                    return Color.FromArgb(a, r, g, b);
                }
            }
            catch { }
            return Colors.White;
        }
        private void OnAnimationChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AnimationComboBox.SelectedItem is ComboBoxItem it) ConfigManager.Instance.Set("page_animation", it.Content.ToString());
        }

        private async void OnPickFontColor(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new ContentDialog
                {
                    Title = "选择字体颜色",
                    PrimaryButtonText = "确定",
                    CloseButtonText = "取消",
                };

                var picker = new ColorPicker
                {
                    IsAlphaEnabled = true,
                    IsAlphaTextInputVisible = true,
                    IsColorChannelTextInputVisible = true,
                    IsColorPreviewVisible = true,
                    IsHexInputVisible = true,
                    IsMoreButtonVisible = false,
                    Margin = new Thickness(0, 8, 0, 0)
                };

                // initialize with current saved color
                var saved = ConfigManager.Instance.Get<string>("font_color", "#FFFFFFFF");
                picker.Color = ParseHexColor(saved);

                dlg.Content = picker;

                // attach to same XamlRoot so dialog is shown correctly
                dlg.XamlRoot = this.XamlRoot;

                var res = await dlg.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    OnFontColorChanged(picker.Color);
                }
            }
            catch { }
        }
        private void OnSingleClickToggled(object sender, RoutedEventArgs e)
        {
            var t = sender as ToggleSwitch; ConfigManager.Instance.Set("file_tree_single_click", t?.IsOn ?? false);
        }
        private void OnDefaultViewChanged(object sender, SelectionChangedEventArgs e)
        {
            ConfigManager.Instance.Set("default_view", DefaultViewComboBox.SelectedIndex == 1 ? "details" : "icon");
        }
        private void OnFontSizeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontSizeComboBox.SelectedItem is ComboBoxItem it)
            {
                var sizeStr = it.Content.ToString();
                // Hot-update font size across the app and persist to config
                if (double.TryParse(sizeStr, out double size))
                    FontService.SetFontSize(size);
            }
        }
        private async void OnSelectFont(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new FontSelectorDialog();
                dlg.XamlRoot = this.XamlRoot;
                var result = await dlg.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(dlg.SelectedFont))
                {
                    // Hot-update font across the app and persist to config
                    FontService.SetFont(dlg.SelectedFont);
                    // Update button label to show the currently active font
                    SelectFontButton.Content = dlg.SelectedFont;
                }
            }
            catch { }
        }
        private void OnFollowCDToggled(object sender, RoutedEventArgs e)
        {
            var t = sender as ToggleSwitch; ConfigManager.Instance.Set("follow_cd", t?.IsOn ?? false);
        }
        private void OnLockRatioToggled(object sender, RoutedEventArgs e)
        {
            var t = sender as ToggleSwitch; ConfigManager.Instance.Set("locked_ratio", t?.IsOn ?? true);
        }
        private void OnClearBackground(object sender, RoutedEventArgs e)
        {
            ConfigManager.Instance.Set("bg_pic", "");
            BackgroundService.SetBackgroundPath("");
        }
        // (removed duplicate recursive placeholder)
        private void OnBackgroundColorChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BackgroundColorComboBox.SelectedItem is ComboBoxItem it) ConfigManager.Instance.Set("bg_color", it.Content.ToString());
        }
        private void OnRightPanelAIChatToggled(object sender, RoutedEventArgs e)
        {
            var t = sender as ToggleSwitch; ConfigManager.Instance.Set("right_panel_ai_chat", t?.IsOn ?? true);
        }
        private void OnUpdateChannelChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UpdateChannelComboBox.SelectedIndex == 0) ConfigManager.Instance.Set("update_channel", "none");
            else if (UpdateChannelComboBox.SelectedIndex == 1) ConfigManager.Instance.Set("update_channel", "stable");
            else ConfigManager.Instance.Set("update_channel", "preview");
        }
        //private void OnTerminalModeChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    ConfigManager.Instance.Set("terminal_mode", TerminalModeComboBox.SelectedIndex);
        //}
        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem it) ConfigManager.Instance.Set("language", it.Content.ToString());
        }
        // (OnPickBackground wired to command above)
        // ------------------------------------------------------------------
    }
}