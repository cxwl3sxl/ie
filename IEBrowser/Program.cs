using System;
using System.Windows.Forms;

namespace IEBrowser
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Feature control 写注册表以 exe 文件名为键，必须在任何浏览器控件
            // 创建之前、且基于持久化设置（含用户选择的 IE 内核版本）执行。
            var settings = new AppSettings();
            settings.Load();

            ActiveXPolicy.IeVersion = settings.IeVersion;
            BrowserFeatureControl.Apply();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                MessageBox.Show(e.Exception.Message, "IE 浏览器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };

            // 兜底：捕获 UI 线程之外（如后台线程、COM 回调漏网）的未处理异常，
            // 至少弹出提示而不是让进程静默崩溃退出。
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                string message = ex != null ? ex.Message : "未知错误";
                try
                {
                    MessageBox.Show(message, "IE 浏览器 - 未处理异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch
                {
                }
            };

            StartupInfo startup = BuildStartup(args);
            Application.Run(new MainForm(startup.InitialUrl, startup.RestoreSession));
        }

        /// <summary>
        /// 计算启动时的初始地址与是否恢复会话：
        ///   1. 命令行带有显式地址 → 只打开该地址，不恢复会话；
        ///   2. --restart 标记（内核切换自动重启）→ 恢复上次会话；
        ///   3. 其余情况 → 恢复上次会话，无会话则空白启动。
        /// </summary>
        private static StartupInfo BuildStartup(string[] args)
        {
            string url = ResolveInitialUrl(args);
            if (!string.IsNullOrEmpty(url))
            {
                return new StartupInfo { InitialUrl = url, RestoreSession = false };
            }

            bool restart = SessionStore.ConsumeRestartFlag();
            return new StartupInfo { InitialUrl = null, RestoreSession = true };
        }

        private sealed class StartupInfo
        {
            public string InitialUrl { get; set; }
            public bool RestoreSession { get; set; }
        }

        /// <summary>
        /// 把命令行参数解析成初始地址。支持：
        ///   IEBrowser.exe http://intranet/report.jsp
        ///   IEBrowser.exe "http://a/b?x=1&amp;y=2"
        ///   IEBrowser.exe C:\page.html            （本地路径，自动转 file:///）
        ///   IEBrowser.exe --url=http://a/b
        /// 多个参数取第一个可识别的地址。
        /// </summary>
        private static string ResolveInitialUrl(string[] args)
        {
            if (args == null) return null;

            foreach (string raw in args)
            {
                if (string.IsNullOrEmpty(raw)) continue;

                string arg = raw.Trim().Trim('"');
                if (arg.Length == 0) continue;

                // 跳过开关类参数（--xxx / -xxx），唯一例外是 --url= 形式
                if (arg.StartsWith("--", StringComparison.Ordinal) ||
                    arg.StartsWith("-", StringComparison.Ordinal))
                {
                    int eq = arg.IndexOf('=');
                    if (eq < 0 || eq == arg.Length - 1) continue;
                    arg = arg.Substring(eq + 1).Trim().Trim('"');
                    if (arg.Length == 0) continue;
                }

                string url = NormalizeToUrl(arg);
                if (url != null) return url;
            }

            return null;
        }

        /// <summary>
        /// 把用户输入规整成可导航的 URL：带协议的保持原样，本地已存在路径转 file:///，
        /// 其余按主机名/内网短名补 http://。
        /// </summary>
        private static string NormalizeToUrl(string arg)
        {
            // 已有协议（http/https/file/ftp/about/res 等）
            int scheme = arg.IndexOf("://", StringComparison.Ordinal);
            if (scheme > 0) return arg;

            if (arg.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return arg;

            // 本地文件路径（绝对路径或已存在的相对路径）
            try
            {
                bool looksLikePath =
                    arg.IndexOf('\\') >= 0 ||
                    arg.StartsWith("/", StringComparison.Ordinal) ||
                    arg.StartsWith(".", StringComparison.Ordinal) ||
                    (arg.Length > 2 && arg[1] == ':');

                if (looksLikePath && System.IO.File.Exists(arg))
                {
                    return new Uri(System.IO.Path.GetFullPath(arg)).AbsoluteUri;
                }
            }
            catch
            {
                // 路径解析失败时按普通地址处理
            }

            return "http://" + arg;
        }
    }
}
