using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace IEBrowser
{
    /// <summary>
    /// 窗口位置等简单配置：%AppData%\IEBrowser\settings.ini。
    /// </summary>
    internal sealed class AppSettings
    {
        private static string FilePath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IEBrowser");
                return Path.Combine(folder, "settings.ini");
            }
        }

        public Rectangle Bounds { get; set; }

        public bool Maximized { get; set; }

        public bool HasBounds { get; set; }

        /// <summary>允许加载并运行 ActiveX 控件。</summary>
        public bool AllowActiveX { get; set; }

        /// <summary>在 Internet 区域也应用上述规则（高风险，默认关闭）。</summary>
        public bool AllowOnInternet { get; set; }

        /// <summary>注入自定义 IInternetSecurityManager 站点（排查用，默认开启）。</summary>
        public bool UseCustomSecurityManager { get; set; }

        /// <summary>旧版 ActiveX 兼容模式（IE7 内核 + 关 GPU + 去主题，默认关闭）。</summary>
        public bool CompatibilityMode { get; set; }

        /// <summary>自动隔离含旧版 ActiveX 的页面（用独立 IE 打开），默认开启。</summary>
        public bool AutoIsolateActiveX { get; set; }

        public AppSettings()
        {
            AllowActiveX = true;
            AllowOnInternet = false;
            UseCustomSecurityManager = true;
            CompatibilityMode = false;
            AutoIsolateActiveX = false;
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;

                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int split = line.IndexOf('=');
                    if (split <= 0) continue;
                    string key = line.Substring(0, split).Trim();
                    string value = line.Substring(split + 1).Trim();

                    if (key == "bounds")
                    {
                        string[] parts = value.Split(',');
                        if (parts.Length == 4)
                        {
                            int x, y, w, h;
                            if (int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y) &&
                                int.TryParse(parts[2], out w) && int.TryParse(parts[3], out h) &&
                                w > 200 && h > 150)
                            {
                                Bounds = new Rectangle(x, y, w, h);
                                HasBounds = true;
                            }
                        }
                    }
                    else if (key == "maximized")
                    {
                        Maximized = value == "1";
                    }
                    else if (key == "activex")
                    {
                        AllowActiveX = value == "1";
                    }
                    else if (key == "activex_internet")
                    {
                        AllowOnInternet = value == "1";
                    }
                    else if (key == "use_custom_security")
                    {
                        UseCustomSecurityManager = value == "1";
                    }
                    else if (key == "compat_mode")
                    {
                        CompatibilityMode = value == "1";
                    }
                    else if (key == "auto_isolate_activex")
                    {
                        AutoIsolateActiveX = value == "1";
                    }
                }
            }
            catch
            {
            }
        }

        public void Save(Rectangle bounds, bool maximized)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string content =
                    "bounds=" + bounds.X + "," + bounds.Y + "," + bounds.Width + "," + bounds.Height +
                    Environment.NewLine +
                    "maximized=" + (maximized ? "1" : "0") + Environment.NewLine +
                    "activex=" + (AllowActiveX ? "1" : "0") + Environment.NewLine +
                    "activex_internet=" + (AllowOnInternet ? "1" : "0") + Environment.NewLine +
                    "use_custom_security=" + (UseCustomSecurityManager ? "1" : "0") + Environment.NewLine +
                    "compat_mode=" + (CompatibilityMode ? "1" : "0") + Environment.NewLine +
                    "auto_isolate_activex=" + (AutoIsolateActiveX ? "1" : "0") + Environment.NewLine;
                File.WriteAllText(FilePath, content);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 保证窗口落在可见屏幕内。
        /// </summary>
        public static bool IsVisibleOnScreen(Rectangle bounds)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle intersect = Rectangle.Intersect(screen.WorkingArea, bounds);
                if (intersect.Width >= 120 && intersect.Height >= 80) return true;
            }
            return false;
        }
    }
}
