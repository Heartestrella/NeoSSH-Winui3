using System;

namespace NeoSSH.Services
{
    public static class FontService
    {
        /// <summary>
        /// Set the global terminal font family and persist to config.
        /// </summary>
        public static void SetFont(string fontFamily)
        {
            try
            {
                var main = App.CurrentWindow as MainWindow;
                main?.SetGlobalFont(fontFamily);
                ConfigManager.Instance.Set("editor_font", fontFamily);
            }
            catch { }
        }

        /// <summary>
        /// Set the global terminal font size and persist to config.
        /// </summary>
        public static void SetFontSize(double size)
        {
            try
            {
                var main = App.CurrentWindow as MainWindow;
                main?.SetGlobalFontSize(size);
                ConfigManager.Instance.Set("font_size", ((int)size).ToString());
            }
            catch { }
        }
    }
}
