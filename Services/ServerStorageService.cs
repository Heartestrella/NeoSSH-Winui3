using NeoSSH.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NeoSSH.Services
{
    public static class ServerStorageService
    {
        private static readonly string _filePath;
        private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

        static ServerStorageService()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NeoSSH");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "servers.json");
        }

        public static List<ServerConfig> Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return new List<ServerConfig>();
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<ServerConfig>>(json) ?? new List<ServerConfig>();
            }
            catch
            {
                return new List<ServerConfig>();
            }
        }

        public static void Save(IEnumerable<ServerConfig> servers)
        {
            try
            {
                var json = JsonSerializer.Serialize(servers, _jsonOpts);
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }

        public static void Add(ServerConfig server, IList<ServerConfig> list)
        {
            list.Add(server);
            Save(list);
        }

        public static void Remove(string id, IList<ServerConfig> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Id == id) { list.RemoveAt(i); break; }
            }
            Save(list);
        }

        public static void Update(ServerConfig updated, IList<ServerConfig> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id == updated.Id) { list[i] = updated; break; }
            }
            Save(list);
        }
    }
}
