using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NeoSSH.Models;
using NeoSSH.Services;
using NeoSSH.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using NeoSSH.Tools;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

namespace NeoSSH
{
    public sealed partial class HomePage : Page
    {
        public ObservableCollection<ServerModel> Servers { get; } = new ObservableCollection<ServerModel>();

        // 内存中的完整配置列表（与 Servers 一一对应）
        private List<ServerConfig> _configs = new List<ServerConfig>();
        private CancellationTokenSource _cts;
        private DispatcherQueue _dispatcher;
        private readonly object _lastPolledLock = new object();
        private readonly Dictionary<string, DateTime> _lastPolled = new Dictionary<string, DateTime>();

        public HomePage()
        {
            this.InitializeComponent();
            LoadSavedServers();
            // start background polling loop
            _dispatcher = this.DispatcherQueue;
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => PollLoopAsync(_cts.Token));

            this.Unloaded += HomePage_Unloaded;
        }

        // ── 从持久化存储加载 ──────────────────────────────────────

        private void LoadSavedServers()
        {
            _configs = ServerStorageService.Load();
            Servers.Clear();
            foreach (var cfg in _configs)
                Servers.Add(ServerModel.FromConfig(cfg));
        }

        // ── 新建会话 ──────────────────────────────────────────────

        private async void NewSession_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NewSessionDialog
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && dialog.Result != null)
            {
                var cfg = dialog.Result;
                ServerStorageService.Add(cfg, _configs);
                Servers.Add(ServerModel.FromConfig(cfg));
            }
        }

        // ── IP 显示切换 ───────────────────────────────────────────

        private void IpToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ServerModel server)
            {
                server.IsIpVisible = !server.IsIpVisible;
            }
        }

        // ── 服务器卡片右键菜单（编辑 / 删除）────────────────────

        private async void EditServer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ServerModel model)
            {
                var cfg = _configs.Find(c => c.Id == model.Id);
                if (cfg == null) return;

                var dialog = new NewSessionDialog(cfg) { XamlRoot = this.XamlRoot };
                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary && dialog.Result != null)
                {
                    var updated = dialog.Result;
                    updated.Id = cfg.Id;
                    updated.CreatedAt = cfg.CreatedAt;

                    ServerStorageService.Update(updated, _configs);

                    var idx = IndexOf(model);
                    if (idx >= 0)
                    {
                        Servers.RemoveAt(idx);
                        Servers.Insert(idx, ServerModel.FromConfig(updated));
                    }
                }
            }
        }

        private void DeleteServer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ServerModel model)
            {
                ServerStorageService.Remove(model.Id, _configs);
                var idx = IndexOf(model);
                if (idx >= 0) Servers.RemoveAt(idx);
            }
        }

        private int IndexOf(ServerModel model)
        {
            for (int i = 0; i < Servers.Count; i++)
                if (Servers[i].Id == model.Id) return i;
            return -1;
        }

        private void HomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            try { _cts?.Cancel(); } catch { }
            _cts?.Dispose();
            _cts = null;
        }

        // 后台轮询，每 5s 获取一次所有服务器信息
        private async Task PollLoopAsync(CancellationToken token)
        {
            var poller = new Server_Info();
            while (!token.IsCancellationRequested)
            {
                // capture snapshots of configs and models on UI thread
                var tcs = new TaskCompletionSource<(List<ServerConfig>, List<ServerModel>)>();
                _dispatcher.TryEnqueue(() => tcs.SetResult((new List<ServerConfig>(_configs), new List<ServerModel>(Servers))));
                var (snapshotConfigs, snapshotModels) = await tcs.Task;

                var modelMap = snapshotModels.ToDictionary(m => m.Id);

                foreach (var cfg in snapshotConfigs)
                {
                    if (token.IsCancellationRequested) break;

                    if (!modelMap.TryGetValue(cfg.Id, out var model)) continue;

                    // check per-server monitoring enabled
                    if (!cfg.EnableMonitoring)
                    {
                        _dispatcher.TryEnqueue(() => { model.Status = ServerModel.ConnectionStatus.Unknown; model.StatusMessage = "Monitoring off"; });
                        continue;
                    }

                    // honor per-server interval
                    var now = DateTime.UtcNow;
                    lock (_lastPolledLock)
                    {
                        if (_lastPolled.TryGetValue(cfg.Id, out var last) && (now - last).TotalSeconds < cfg.MonitorIntervalSeconds)
                        {
                            // skip this server this round
                            continue;
                        }
                        _lastPolled[cfg.Id] = now;
                    }

                    try
                    {
                        // perform SSH and info retrieval on background thread
                        await Task.Run(() =>
                        {
                            using var client = Server_Info.Loginto(cfg.Host, cfg.Port, cfg.Username, cfg.AuthType == Models.AuthType.PrivateKey ? cfg.PrivateKeyPath : cfg.Password);
                            try
                            {
                                client.Connect();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[Poll] Connect failed for {cfg.Name}@{cfg.Host}:{cfg.Port} - {ex}");
                                throw; // rethrow to be caught by outer catch
                            }

                            var info = poller.GetInfo(client);
                            client.Disconnect();

                            double cpu = 0, ram = 0, disk = 0;

                            if (info.TryGetValue("cpu_percent", out var cpuObj) && double.TryParse(cpuObj?.ToString(), out var dcpu)) cpu = dcpu;
                            if (info.TryGetValue("memory", out var memObj) && memObj is Dictionary<string, object> memDict)
                            {
                                if (memDict.TryGetValue("used_percent", out var usedPct) && double.TryParse(usedPct?.ToString(), out var dram)) ram = dram;
                            }
                            if (info.TryGetValue("disk", out var diskObj) && diskObj is Dictionary<string, object> diskDict)
                            {
                                if (diskDict.TryGetValue("used_percent_raw", out var dp) && double.TryParse(dp?.ToString(), out var ddisk)) disk = ddisk;
                                else if (diskDict.TryGetValue("used_percent", out var dp2) && double.TryParse(dp2?.ToString(), out var ddisk2)) disk = ddisk2;
                            }

                            // if poller returned an error entry, treat as failure
                            if (info.TryGetValue("error", out var errObj) && errObj != null)
                            {
                                var emsg = errObj.ToString();
                                Debug.WriteLine($"[Poll] Poller error for {cfg.Name}@{cfg.Host}: {emsg}");
                                _dispatcher.TryEnqueue(() =>
                                {
                                    model.Status = ServerModel.ConnectionStatus.Error;
                                    model.StatusMessage = emsg;
                                });
                            }
                            else
                            {
                                // update model on UI thread
                                _dispatcher.TryEnqueue(() =>
                                {
                                    model.CpuUsage = cpu;
                                    model.RamUsage = ram;
                                    model.DiskUsage = disk;
                                    model.Status = ServerModel.ConnectionStatus.Ok;
                                    model.StatusMessage = $"OK ({DateTime.Now:HH:mm:ss})";
                                });
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        var msg = ex.Message;
                        Debug.WriteLine($"[Poll] Exception for {cfg.Name}@{cfg.Host}:{cfg.Port} - {ex}");
                        _dispatcher.TryEnqueue(() =>
                        {
                            model.Status = ServerModel.ConnectionStatus.Error;
                            model.StatusMessage = msg;
                        });
                    }
                }

                try { await Task.Delay(TimeSpan.FromSeconds(5), token); } catch { break; }
            }
        }
    }

    // ── ServerModel ───────────────────────────────────────────────

    public class ServerModel : INotifyPropertyChanged
    {
        // 对应 ServerConfig.Id，用于定位和删除
        public string Id { get; set; }
        public string Name { get; set; }
        public string Host { get; set; }

        // RawIp 兼容旧字段，实际存储 Host 值
        public string RawIp => Host;

        private bool _isIpVisible = false;
        public bool IsIpVisible
        {
            get => _isIpVisible;
            set
            {
                _isIpVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayIp));
            }
        }

        public string DisplayIp => IsIpVisible ? RawIp : "***.***.***.***";

        // --- 资源监控数据预留（未来接 SSH 实时数据）---

        private double _cpuUsage;
        public double CpuUsage
        {
            get => _cpuUsage;
            set { _cpuUsage = value; OnPropertyChanged(); OnPropertyChanged(nameof(CpuUsageText)); }
        }
        public string CpuUsageText => $"{CpuUsage:F0}%";

        private double _ramUsage;
        public double RamUsage
        {
            get => _ramUsage;
            set { _ramUsage = value; OnPropertyChanged(); OnPropertyChanged(nameof(RamUsageText)); }
        }
        public string RamUsageText => $"{RamUsage:F0}%";

        private double _diskUsage;
        public double DiskUsage
        {
            get => _diskUsage;
            set { _diskUsage = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiskUsageText)); }
        }
        public string DiskUsageText => $"{DiskUsage:F0}%";

        // 连接状态灯
        public enum ConnectionStatus { Unknown, Ok, Error }

        private ConnectionStatus _status = ConnectionStatus.Unknown;
        public ConnectionStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusBrush)); }
        }

        // 供 XAML 绑定的 Brush
        public SolidColorBrush StatusBrush
        {
            get
            {
                return _status switch
                {
                    ConnectionStatus.Ok => new SolidColorBrush(Colors.Green), // green
                    ConnectionStatus.Error => new SolidColorBrush(Colors.Red), // red
                    _ => new SolidColorBrush(Colors.Gray), // gray
                };
            }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        // 属性通知
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 从 ServerConfig 构建 ServerModel
        public static ServerModel FromConfig(ServerConfig cfg) => new ServerModel
        {
            Id = cfg.Id,
            Name = cfg.Name,
            Host = cfg.Host,
            CpuUsage = 0,
            RamUsage = 0,
            DiskUsage = 0,
            Status = ConnectionStatus.Unknown
        };
    }
}
