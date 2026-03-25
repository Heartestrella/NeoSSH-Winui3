using System;
using Microsoft.UI.Xaml;

namespace NeoSSH.Services
{
    public static class BackgroundService
    {
        /// <summary>
        /// Set global background image by file path. MainWindow must be created.
        /// </summary>
        public static void SetBackgroundPath(string path)
        {
            try
            {
                var main = App.CurrentWindow as MainWindow;
                main?.SetGlobalBackground(path);

                // Persist to config (store as string path)
                try
                {
                    ConfigManager.Instance.Set("bg_pic", path);
                    System.Diagnostics.Debug.WriteLine($"Persisted bg_pic={path} to {ConfigManager.Instance.GetConfigPath()}");
                    // verify by reading back
                    try
                    {
                        var saved = ConfigManager.Instance.Get<string>("bg_pic", "");
                        System.Diagnostics.Debug.WriteLine($"Read back bg_pic={saved}");
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to read back config: {ex2.Message}");
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to persist bg_pic: {ex.Message}"); }
            }
            catch
            {
                // ignore
            }
        }

        public static void SetBackgroundOpacity(double opacity)
        {
            try
            {
                var main = App.CurrentWindow as MainWindow;
                main?.SetGlobalBackgroundOpacity(opacity);

                // Persist opacity as integer percentage (0-100)
                try
                {
                    int pct = (int)Math.Round(opacity * 100.0);
                    ConfigManager.Instance.Set("background_opacity", pct);
                    System.Diagnostics.Debug.WriteLine($"Persisted background_opacity={pct} to {ConfigManager.Instance.GetConfigPath()}");
                    // verify by reading back
                    try
                    {
                        var saved = ConfigManager.Instance.Get<int>("background_opacity", 0);
                        System.Diagnostics.Debug.WriteLine($"Read back background_opacity={saved}");
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to read back config: {ex2.Message}");
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to persist background_opacity: {ex.Message}"); }
            }
            catch
            {
                // ignore
            }
        }

        public static double GetBackgroundOpacity()
        {
            try
            {
                var main = App.CurrentWindow as MainWindow;
                return main?.GetGlobalBackgroundOpacity() ?? 1.0;
            }
            catch
            {
                return 1.0;
            }
        }
    }
}
