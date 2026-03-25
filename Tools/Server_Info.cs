using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoSSH.Tools
{
    internal class Server_Info
    {
        public Dictionary<string, object> GetInfo(SshClient client)
        {
            var info = new Dictionary<string, object>();

            if (!client.IsConnected)
            {
                info["error"] = "SSH client not connected";
                return info;
            }

            info["cpu_percent"] = GetCpuUsage(client);
            info["memory"] = GetMemoryUsage(client);
            info["disk"] = GetDiskUsage(client);

            return info;
        }

        private double GetCpuUsage(SshClient client)
        {
            try
            {
                var command = client.RunCommand("top -bn1 | grep 'Cpu(s)' | awk '{print $2}' | cut -d'%' -f1");
                string result = command.Result.Trim();

                if (!string.IsNullOrEmpty(result) && double.TryParse(result, out double cpuUsage))
                {
                    return Math.Round(cpuUsage, 2);
                }

                command = client.RunCommand("mpstat 1 1 | awk 'END{print 100 - $NF}'");
                result = command.Result.Trim();

                if (!string.IsNullOrEmpty(result) && double.TryParse(result, out cpuUsage))
                {
                    return Math.Round(cpuUsage, 2);
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private Dictionary<string, object> GetMemoryUsage(SshClient client)
        {
            var memoryInfo = new Dictionary<string, object>();

            try
            {
                var command = client.RunCommand("free -m | awk '/^Mem:/ {print $2,$3,$4,$7}'");
                string result = command.Result.Trim();

                if (!string.IsNullOrEmpty(result))
                {
                    var parts = result.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        long total = long.Parse(parts[0]);
                        long used = long.Parse(parts[1]);
                        long free = long.Parse(parts[2]);
                        long available = long.Parse(parts[3]);

                        memoryInfo["total_mb"] = total;
                        memoryInfo["used_mb"] = used;
                        memoryInfo["free_mb"] = free;
                        memoryInfo["available_mb"] = available;
                        memoryInfo["used_percent"] = Math.Round((double)used / total * 100, 2);
                        memoryInfo["available_percent"] = Math.Round((double)available / total * 100, 2);
                    }
                }
            }
            catch { }

            return memoryInfo;
        }

        private Dictionary<string, object> GetDiskUsage(SshClient client)
        {
            var diskInfo = new Dictionary<string, object>();

            try
            {
                var command = client.RunCommand("df -h / | awk 'NR==2 {print $1,$2,$3,$4,$5}'");
                string result = command.Result.Trim();

                if (!string.IsNullOrEmpty(result))
                {
                    var parts = result.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        diskInfo["device"] = parts[0];
                        diskInfo["total"] = parts[1];
                        diskInfo["used"] = parts[2];
                        diskInfo["available"] = parts[3];
                        diskInfo["used_percent"] = parts[4].Replace("%", "");
                    }
                }

                command = client.RunCommand("df -B1 / | awk 'NR==2 {print $2,$3,$5}' | tr -d '%'");
                result = command.Result.Trim();

                if (!string.IsNullOrEmpty(result))
                {
                    var parts = result.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        diskInfo["total_bytes"] = long.Parse(parts[0]);
                        diskInfo["used_bytes"] = long.Parse(parts[1]);
                        diskInfo["used_percent_raw"] = double.Parse(parts[2]);
                    }
                }
            }
            catch { }

            return diskInfo;
        }


        public static SshClient Loginto(string ip, int port, string username, string password) // password may be a path of private key file
        {
            SshClient client;
            if (IsValidPath(password) && File.Exists(password))
            {
                var keyFile = new PrivateKeyFile(password);
                client = new SshClient(ip, port, username, keyFile);
            }
            else
            {
                client = new SshClient(ip, port, username, password);
            }


            return client;
        }

        private static bool IsValidPath(string path)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);

                if (Path.GetInvalidPathChars().Any(c => path.Contains(c)))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
