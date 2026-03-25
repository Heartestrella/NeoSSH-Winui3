using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NeoSSH.Services
{
    public sealed class ConfigManager
    {
        private static ConfigManager _instance;
        private static readonly object _lock = new object();

        private readonly string _configPath;
        private Dictionary<string, object> _data;

        private readonly Dictionary<string, object> _defaults = new()
        {
            ["bg_pic"] = "",
            ["background_opacity"] = 35,
            ["bg_color"] = "Dark",
            ["language"] = "system",
            ["terminal_mode"] = 0,
            ["update_channel"] = "none",
            ["right_panel_ai_chat"] = true,
            ["locked_ratio"] = true,
            ["follow_cd"] = false,
            ["font_size"] = "12",
            ["default_view"] = "icon",
            ["file_tree_single_click"] = false,
            ["page_animation"] = "slide_fade",
            ["max_concurrent_transfers"] = 10,
            ["external_editor"] = "",
            ["editor_font"] = "",
            ["font_color"] = "#FFFFFFFF"
        };

        private ConfigManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "NeoSSH");
            Directory.CreateDirectory(dir);
            _configPath = Path.Combine(dir, "setting-config.json");
            _data = new Dictionary<string, object>(_defaults);
            Load();
        }

        public static ConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null) _instance = new ConfigManager();
                    }
                }
                return _instance;
            }
        }

        public string GetConfigPath() => _configPath;

        private void Load()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    Save();
                    return;
                }
                var json = File.ReadAllText(_configPath);
                var doc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (doc != null)
                {
                    foreach (var kv in _defaults)
                    {
                        if (!doc.ContainsKey(kv.Key)) doc[kv.Key] = kv.Value;
                    }
                    _data = doc;
                }
            }
            catch
            {
                _data = new Dictionary<string, object>(_defaults);
            }
        }

        private void Save()
        {
            try
            {
                var opts = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_data, opts);
                File.WriteAllText(_configPath, json);
            }
            catch
            {
            }
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            try
            {
                if (_data.TryGetValue(key, out var v))
                {
                    if (v is JsonElement je)
                    {
                        switch (je.ValueKind)
                        {
                            case JsonValueKind.String:
                                if (typeof(T) == typeof(string)) return (T)(object)je.GetString();
                                return (T)Convert.ChangeType(je.GetString(), typeof(T));
                            case JsonValueKind.Number:
                                if (typeof(T) == typeof(int)) return (T)(object)je.GetInt32();
                                if (typeof(T) == typeof(double)) return (T)(object)je.GetDouble();
                                return (T)Convert.ChangeType(je.GetDouble(), typeof(T));
                            case JsonValueKind.True:
                            case JsonValueKind.False:
                                if (typeof(T) == typeof(bool)) return (T)(object)je.GetBoolean();
                                return (T)Convert.ChangeType(je.GetBoolean(), typeof(T));
                            default:
                                break;
                        }
                    }
                    return (T)Convert.ChangeType(v, typeof(T));
                }
            }
            catch { }
            return defaultValue;
        }

        public void Set<T>(string key, T value)
        {
            try
            {
                _data[key] = value;
                Save();
            }
            catch { }
        }
    }
}
