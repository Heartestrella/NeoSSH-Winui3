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
                // Attempt to read load averages from /proc/loadavg
                // /proc/loadavg fields: 1min 5min 15min ...
                // Linux doesn't provide a 3-minute value; we approximate the 3-minute average
                // by interpolating between the 1-minute and 5-minute averages: avg3 ~= (1min + 5min) / 2
                var cmd = client.RunCommand("cat /proc/loadavg");
                var res = cmd.Result?.Trim();
                if (!string.IsNullOrEmpty(res))
                {
                    var parts = res.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && double.TryParse(parts[0], out var oneMin) && double.TryParse(parts[1], out var fiveMin))
                    {
                        var approx3MinLoad = (oneMin + fiveMin) / 2.0; // approximation for 3-minute average

                        // get number of CPU cores to normalize load to percentage
                        int cores = 1;
                        try
                        {
                            var ccmd = client.RunCommand("nproc");
                            var cres = ccmd.Result?.Trim();
                            if (!string.IsNullOrEmpty(cres) && int.TryParse(cres, out var n)) cores = Math.Max(1, n);
                            else
                            {
                                // fallback: count processors from /proc/cpuinfo
                                var ccmd2 = client.RunCommand("grep -c ^processor /proc/cpuinfo");
                                var cres2 = ccmd2.Result?.Trim();
                                if (!string.IsNullOrEmpty(cres2) && int.TryParse(cres2, out var n2)) cores = Math.Max(1, n2);
                            }
                        }
                        catch { cores = 1; }

                        // Convert load average to percentage relative to cores
                        var percent = (approx3MinLoad / cores) * 100.0;
                        if (percent < 0) percent = 0;
                        // Do not artificially cap at 100; load can exceed cores (but UI progress bars may expect <=100)
                        return Math.Round(percent, 2);
                    }
                }

                // Fallback: try mpstat quick sample (may not exist on all systems)
                try
                {
                    var mp = client.RunCommand("mpstat 1 1 | awk 'END{print 100 - $NF}'");
                    var mres = mp.Result?.Trim();
                    if (!string.IsNullOrEmpty(mres) && double.TryParse(mres, out var cpuUsage))
                        return Math.Round(cpuUsage, 2);
                }
                catch { }

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
