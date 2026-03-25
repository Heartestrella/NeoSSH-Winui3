using System;

namespace NeoSSH.Models
{
    public enum AuthType
    {
        Password,
        PrivateKey
    }

    public class ServerConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string PrivateKeyPath { get; set; } = "";
        public AuthType AuthType { get; set; } = AuthType.Password;
        public string Group { get; set; } = "";
        public string Remark { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        // 是否在主页面开启性能监控
        public bool EnableMonitoring { get; set; } = false;
        // 监控间隔（秒），允许 3 - 10
        public int MonitorIntervalSeconds { get; set; } = 5;
    }
}
