using System;
using System.Windows.Forms;

namespace IEBrowser
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
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

            Application.Run(new MainForm());
        }
    }
}
