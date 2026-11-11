using System;
using System.Windows.Forms;

namespace IEBrowser
{
    /// <summary>
    /// 封装一个标签页中的 WebBrowser，并接管弹出窗口事件。
    /// </summary>
    internal sealed class BrowserTab : IDisposable
    {
        private readonly NewWindowSink _sink;
        private IConnectionPoint _connectionPoint;
        private int _cookie = -1;
        private bool _disposed;

        public BrowserTab()
        {
            Title = "新标签页";
            Url = string.Empty;

            Browser = new ActiveXBrowser();
            Browser.Dock = DockStyle.Fill;
            Browser.ScriptErrorsSuppressed = true;
            Browser.AllowWebBrowserDrop = true;
            Browser.WebBrowserShortcutsEnabled = true;
            Browser.IsWebBrowserContextMenuEnabled = true;
            Browser.AllowNavigation = true;
            Browser.Navigated += OnNavigated;
            Browser.Navigated += delegate
            {
                if (_connectionPoint == null) HookNewWindow();
            };
            Browser.NewWindow += OnNewWindow;

            _sink = new NewWindowSink(OnNewWindowRequested);
        }

        public WebBrowser Browser { get; private set; }

        public string Title { get; set; }

        public string Url { get; set; }

        public bool IsLoading { get; set; }

        public event Action<string> PopupRequested;

        public void Navigate(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                Browser.Navigate(url);
            }
            catch
            {
                // 忽略导航异常（非法地址等）。
            }
        }

        private void OnNavigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            try
            {
                if (Browser.Url != null)
                {
                    Url = Browser.Url.ToString();
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 兜底：阻止 WebBrowser 默认启动外部 IE 窗口。
        /// </summary>
        private void OnNewWindow(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
        }

        private void HookNewWindow()
        {
            try
            {
                object activeX = Browser.ActiveXInstance;
                if (activeX == null) return;

                var container = activeX as IConnectionPointContainer;
                if (container == null) return;

                Guid diid = typeof(DWebBrowserEvents2).GUID;
                IConnectionPoint point;
                container.FindConnectionPoint(ref diid, out point);
                if (point == null) return;

                int cookie;
                if (point.Advise(_sink, out cookie) == 0)
                {
                    _connectionPoint = point;
                    _cookie = cookie;
                }
            }
            catch
            {
                _connectionPoint = null;
                _cookie = -1;
            }
        }

        private void OnNewWindowRequested(string url)
        {
            Action<string> handler = PopupRequested;
            if (handler != null) handler(url);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_connectionPoint != null && _cookie >= 0)
                {
                    _connectionPoint.Unadvise(_cookie);
                }
            }
            catch
            {
            }

            _connectionPoint = null;
            _cookie = -1;

            try
            {
                Browser.Navigated -= OnNavigated;
                Browser.NewWindow -= OnNewWindow;
                Browser.Dispose();
            }
            catch
            {
            }
        }
    }
}
