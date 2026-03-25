using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NeoSSH.Models;
using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NeoSSH.Views
{
    /// <summary>
    /// 新建/编辑会话对话框（程序化构建，无需 XAML 文件）
    /// </summary>
    public class NewSessionDialog : ContentDialog
    {
        public ServerConfig Result { get; private set; }

        private readonly TextBox _nameBox;
        private readonly ToggleSwitch _enableMonitoringSwitch;
        private readonly NumberBox _monitorIntervalBox;
        private readonly TextBox _hostBox;
        private readonly NumberBox _portBox;
        private readonly TextBox _usernameBox;
        private readonly PasswordBox _passwordBox;
        private readonly TextBox _privateKeyBox;
        private readonly ComboBox _authTypeBox;
        private readonly TextBox _groupBox;
        private readonly TextBox _remarkBox;
        private readonly StackPanel _passwordPanel;
        private readonly StackPanel _keyPanel;
        private readonly TextBlock _errorText;

        public NewSessionDialog(ServerConfig existing = null)
        {
            this.Title = existing == null ? "新建会话" : "编辑会话";
            this.PrimaryButtonText = "保存";
            this.SecondaryButtonText = "取消";
            this.DefaultButton = ContentDialogButton.Primary;
            this.MinWidth = 480;

            // ── 根布局：单列 ScrollViewer 包裹 StackPanel ──
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 600
            };

            var panel = new StackPanel { Spacing = 12, Padding = new Thickness(2, 4, 2, 4) };

            // ── 显示名称 ──
            panel.Children.Add(MakeLabel("会话名称"));
            _nameBox = new TextBox { PlaceholderText = "例如：生产环境 Web-01" };
            panel.Children.Add(_nameBox);

            // ── 主机 / 端口（同行）──
            panel.Children.Add(MakeLabel("主机地址与端口"));
            var hostRow = new Grid { ColumnSpacing = 8 };
            hostRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            hostRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _hostBox = new TextBox { PlaceholderText = "IP 或域名" };
            _hostBox.TextChanged += (s, e) => SetHostError(false);
            Grid.SetColumn(_hostBox, 0);
            _portBox = new NumberBox
            {
                PlaceholderText = "端口",
                Minimum = 1,
                Maximum = 65535,
                Value = 22,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden
            };
            Grid.SetColumn(_portBox, 1);
            hostRow.Children.Add(_hostBox);
            hostRow.Children.Add(_portBox);
            panel.Children.Add(hostRow);

            // ── 用户名 ──
            panel.Children.Add(MakeLabel("用户名"));
            _usernameBox = new TextBox { PlaceholderText = "例如：root" };
            panel.Children.Add(_usernameBox);

            // ── 认证方式 ──
            panel.Children.Add(MakeLabel("认证方式"));
            _authTypeBox = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _authTypeBox.Items.Add("密码认证");
            _authTypeBox.Items.Add("私钥认证");
            _authTypeBox.SelectedIndex = 0;   // 必须在 Add 之后才能设置
            _authTypeBox.SelectionChanged += AuthTypeBox_SelectionChanged;
            panel.Children.Add(_authTypeBox);

            // ── 密码面板 ──
            _passwordPanel = new StackPanel { Spacing = 8 };
            _passwordPanel.Children.Add(MakeLabel("密码"));
            _passwordBox = new PasswordBox { PlaceholderText = "SSH 登录密码" };
            _passwordPanel.Children.Add(_passwordBox);
            panel.Children.Add(_passwordPanel);

            // ── 私钥面板（初始隐藏）──
            _keyPanel = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed };
            _keyPanel.Children.Add(MakeLabel("私钥文件路径"));
            var keyRow = new Grid { ColumnSpacing = 8 };
            keyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            keyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _privateKeyBox = new TextBox { PlaceholderText = "例如：C:\\Users\\xxx\\.ssh\\id_rsa", IsReadOnly = true };
            Grid.SetColumn(_privateKeyBox, 0);
            var browseBtn = new Button { Content = "浏览…" };
            browseBtn.Click += BrowseKey_Click;
            Grid.SetColumn(browseBtn, 1);
            keyRow.Children.Add(_privateKeyBox);
            keyRow.Children.Add(browseBtn);
            _keyPanel.Children.Add(keyRow);
            panel.Children.Add(_keyPanel);

            // ── 分组 ──
            panel.Children.Add(MakeLabel("分组（可选）"));
            _groupBox = new TextBox { PlaceholderText = "例如：生产环境" };
            panel.Children.Add(_groupBox);

            // ── 备注 ──
            panel.Children.Add(MakeLabel("备注（可选）"));
            _remarkBox = new TextBox
            {
                PlaceholderText = "填写说明信息…",
                AcceptsReturn = true,
                MaxHeight = 80,
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(_remarkBox);

            // ── 监控开关 ──
            panel.Children.Add(MakeLabel("在主页面开启性能监控"));
            var monRow = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) }, new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } } };
            _enableMonitoringSwitch = new ToggleSwitch { HorizontalAlignment = HorizontalAlignment.Left };
            _enableMonitoringSwitch.Toggled += (s, e) =>
            {
                _monitorIntervalBox.Visibility = _enableMonitoringSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
            };
            Grid.SetColumn(_enableMonitoringSwitch, 0);
            _monitorIntervalBox = new NumberBox { Minimum = 3, Maximum = 10, Value = 5, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, HorizontalAlignment = HorizontalAlignment.Left, Width = 100, Visibility = Visibility.Collapsed };
            Grid.SetColumn(_monitorIntervalBox, 1);
            monRow.Children.Add(_enableMonitoringSwitch);
            monRow.Children.Add(_monitorIntervalBox);
            panel.Children.Add(monRow);

            // ── 错误提示（内联，不弹第二个 Dialog）──
            _errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 4, 0, 0)
            };
            panel.Children.Add(_errorText);

            scroll.Content = panel;
            this.Content = scroll;

            // 若是编辑模式，回填已有数据
            if (existing != null) FillFrom(existing);

            this.PrimaryButtonClick += OnPrimaryButtonClick;
        }

        // ── 事件处理 ─────────────────────────────────────────────

        private void AuthTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool isKey = _authTypeBox.SelectedIndex == 1;
            _passwordPanel.Visibility = isKey ? Visibility.Collapsed : Visibility.Visible;
            _keyPanel.Visibility = isKey ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void BrowseKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                var hwnd = WindowNative.GetWindowHandle(App.CurrentWindow);
                InitializeWithWindow.Initialize(picker, hwnd);
                picker.FileTypeFilter.Add("*");
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                var file = await picker.PickSingleFileAsync();
                if (file != null) _privateKeyBox.Text = file.Path;
            }
            catch { }
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 验证必填字段
            if (string.IsNullOrWhiteSpace(_hostBox.Text))
            {
                args.Cancel = true;
                SetHostError(true);
                _hostBox.Focus(FocusState.Programmatic);
                ShowError("主机地址不能为空");
                return;
            }
            if (!IsValidHost(_hostBox.Text.Trim()))
            {
                args.Cancel = true;
                SetHostError(true);
                _hostBox.Focus(FocusState.Programmatic);
                ShowError("主机地址格式不正确，请输入合法的 IPv4、IPv6 或域名\n示例：192.168.1.1 / [::1] / my-server.example.com");
                return;
            }
            SetHostError(false);
            if (string.IsNullOrWhiteSpace(_usernameBox.Text))
            {
                args.Cancel = true;
                _usernameBox.Focus(FocusState.Programmatic);
                ShowError("用户名不能为空");
                return;
            }

            HideError();
            var authType = _authTypeBox.SelectedIndex == 1 ? AuthType.PrivateKey : AuthType.Password;

            Result = new ServerConfig
            {
                Name = string.IsNullOrWhiteSpace(_nameBox.Text)
                    ? _hostBox.Text.Trim()
                    : _nameBox.Text.Trim(),
                Host = _hostBox.Text.Trim(),
                Port = double.IsNaN(_portBox.Value) ? 22 : (int)_portBox.Value,
                Username = _usernameBox.Text.Trim(),
                Password = _passwordBox.Password,
                PrivateKeyPath = _privateKeyBox.Text.Trim(),
                AuthType = authType,
                Group = _groupBox.Text.Trim(),
                Remark = _remarkBox.Text.Trim(),
                EnableMonitoring = _enableMonitoringSwitch.IsOn,
                MonitorIntervalSeconds = (int)Math.Round(_monitorIntervalBox.Value)
            };
        }

        // ── 辅助方法 ──────────────────────────────────────────────

        private void FillFrom(ServerConfig s)
        {
            _nameBox.Text = s.Name;
            _hostBox.Text = s.Host;
            _portBox.Value = s.Port;
            _usernameBox.Text = s.Username;
            _passwordBox.Password = s.Password;
            _privateKeyBox.Text = s.PrivateKeyPath;
            _authTypeBox.SelectedIndex = s.AuthType == AuthType.PrivateKey ? 1 : 0;
            _groupBox.Text = s.Group;
            _remarkBox.Text = s.Remark;

            _enableMonitoringSwitch.IsOn = s.EnableMonitoring;
            _monitorIntervalBox.Value = s.MonitorIntervalSeconds;
            _monitorIntervalBox.Visibility = s.EnableMonitoring ? Visibility.Visible : Visibility.Collapsed;

            // 触发一次面板切换
            bool isKey = s.AuthType == AuthType.PrivateKey;
            _passwordPanel.Visibility = isKey ? Visibility.Collapsed : Visibility.Visible;
            _keyPanel.Visibility = isKey ? Visibility.Visible : Visibility.Collapsed;

            // 编辑模式保留原 Id 和 CreatedAt
            Result = new ServerConfig { Id = s.Id, CreatedAt = s.CreatedAt };
        }

        private static TextBlock MakeLabel(string text) =>
            new TextBlock
            {
                Text = text,
                FontSize = 12,
                Opacity = 0.8,
                Margin = new Thickness(0, 4, 0, 0)
            };

        // ── 主机地址校验（对齐 valid_ip.py 逻辑）────────────────

        /// <summary>
        /// 严格校验：IPv4 / IPv6 / 域名，逻辑与 valid_ip.py 一致。
        /// 额外支持 SSH 常用的 localhost（单标签）和 IPv6 括号写法 [::1]。
        /// </summary>
        private static bool IsValidHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            host = host.Trim();

            // ── 处理可选端口 host:port（非 IPv6）──────────────────
            // 仅当有且只有一个冒号且不含 ] 时，认为是 host:port
            if (host.Contains(':') && !host.Contains(']'))
            {
                if (host.Count(c => c == ':') == 1)
                {
                    var parts = host.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int p) && p >= 0 && p <= 65535)
                        host = parts[0];
                }
            }

            // ── 去掉 IPv6 外层括号 [::1] → ::1 ───────────────────
            if (host.StartsWith('[') && host.EndsWith(']'))
                host = host[1..^1];

            // ── 依次尝试：IPv4 → IPv6 → 域名 ─────────────────────
            return IsValidIpv4(host) || IsValidIpv6(host) || IsValidDomain(host);
        }

        /// <summary>
        /// 严格 IPv4：必须4段、纯数字、无前导零、每段 0-255。
        /// 对应 valid_ip.py _is_valid_ipv4
        /// </summary>
        private static bool IsValidIpv4(string addr)
        {
            if (addr.Count(c => c == '.') != 3) return false;
            var parts = addr.Split('.');
            if (parts.Length != 4) return false;

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) return false;
                // 必须全部是数字字符
                foreach (char c in part)
                    if (c < '0' || c > '9') return false;
                // 拒绝前导零（"01"、"00" 等），单个 "0" 允许
                if (part.Length > 1 && part[0] == '0') return false;
                if (!int.TryParse(part, out int num) || num < 0 || num > 255) return false;
            }
            return true;
        }

        /// <summary>
        /// IPv6 校验：用 IPAddress.TryParse + AddressFamily 过滤。
        /// SSH 场景保留 loopback (::1) 和 unspecified (::)。
        /// 对应 valid_ip.py _is_valid_ipv6
        /// </summary>
        private static bool IsValidIpv6(string addr)
        {
            // IPv4 地址不能被误判为 IPv6
            if (IsValidIpv4(addr)) return false;
            return IPAddress.TryParse(addr, out var ip)
                && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
        }

        /// <summary>
        /// 严格域名：总长 ≤ 253，至少两段，每段 1-63 字符，
        /// TLD ≥ 2 字符且含字母，不能以连字符开头/结尾，无连续点。
        /// 额外允许 "localhost"（SSH 常用单标签主机名）。
        /// 对应 valid_ip.py _is_valid_domain
        /// </summary>
        private static bool IsValidDomain(string addr)
        {
            // SSH 特殊单标签：localhost / 纯字母数字短主机名
            if (addr.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;

            if (addr.Length > 253) return false;
            if (addr.StartsWith('.') || addr.EndsWith('.')) return false;
            if (addr.Contains("..")) return false;

            var labels = addr.Split('.');
            if (labels.Length < 2) return false;   // 至少需要二级域名 + TLD

            var labelPattern = new Regex(@"^[a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?$");
            foreach (var label in labels)
            {
                if (string.IsNullOrEmpty(label)) return false;
                if (label.Length > 63) return false;
                // 单字符标签单独验证（正则要求首尾相同字符时长度>=2）
                if (label.Length == 1)
                {
                    if (!char.IsLetterOrDigit(label[0])) return false;
                }
                else
                {
                    if (!labelPattern.IsMatch(label)) return false;
                }
                if (label.StartsWith('-') || label.EndsWith('-')) return false;
            }

            // TLD 至少 2 字符且必须包含字母
            var tld = labels[^1];
            if (tld.Length < 2 || !tld.Any(char.IsLetter)) return false;

            return true;
        }

        /// <summary>
        /// 给主机输入框设置/清除红色错误边框
        /// </summary>
        private void SetHostError(bool hasError)
        {
            if (hasError)
                _hostBox.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Red);
            else
                _hostBox.ClearValue(TextBox.BorderBrushProperty);
        }

        private void ShowError(string message)
        {
            _errorText.Text = message;
            _errorText.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            _errorText.Visibility = Visibility.Collapsed;
        }
    }
}
