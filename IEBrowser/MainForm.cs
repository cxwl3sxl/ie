using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace IEBrowser
{
    public sealed class MainForm : Form
    {
        private const string SearchUrl = "https://www.bing.com/search?q=";
        private const int WmSetIcon = 0x0080;
        private const int IconSmall = 0;
        private const int IconBig = 1;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static readonly Regex SchemeRegex =
            new Regex(@"^[a-zA-Z][a-zA-Z0-9+.\-]*:", RegexOptions.Compiled);

        private static readonly Regex LocalHostRegex =
            new Regex(@"^(localhost|127\.0\.0\.1|\[::1\])(:\d+)?(/.*)?$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DomainRegex =
            new Regex(@"^[a-zA-Z0-9\u4e00-\u9fa5\-]+(\.[a-zA-Z0-9\u4e00-\u9fa5\-]+)+(:\d+)?(/.*)?$",
                RegexOptions.Compiled);

        private readonly List<BrowserTab> _tabs = new List<BrowserTab>();
        private readonly List<string> _history = new List<string>();
        private readonly FavoritesStore _favorites = new FavoritesStore();
        private readonly AppSettings _settings = new AppSettings();
        private readonly ToolTip _toolTip = new ToolTip();
        private readonly Timer _statusTimer = new Timer();

        private int _activeIndex = -1;
        private string _homeUrl = "about:blank";

        private ToolbarPanel _toolbar;
        private GlyphButton _backButton;
        private GlyphButton _forwardButton;
        private GlyphButton _refreshButton;
        private GlyphButton _homeButton;
        private GlyphButton _starButton;
        private GlyphButton _menuButton;
        private AddressBar _addressBar;
        private TabStrip _tabBar;
        private Panel _content;
        private ProgressLine _progress;
        private SimpleStatusBar _statusBar;
        private Icon _smallIcon;
        private Icon _largeIcon;

        public MainForm() : this(null)
        {
        }

        /// <summary>
        /// 支持从命令行携带初始地址启动（浏览器命令行参数 / 文件关联）。
        /// </summary>
        public MainForm(string initialUrl)
        {
            _favorites.Load();
            _settings.Load();

            // 在任何浏览器控件创建之前应用 ActiveX 放行策略
            ActiveXPolicy.AllowActiveX = _settings.AllowActiveX;
            ActiveXPolicy.AllowOnInternet = _settings.AllowOnInternet;
            ActiveXPolicy.UseCustomSecurityManager = _settings.UseCustomSecurityManager;
            ActiveXPolicy.AutoIsolateActiveX = _settings.AutoIsolateActiveX;
            _homeUrl = StartPage.Create();

            InitializeUi();
            RestoreWindowBounds();

            _statusTimer.Interval = 2600;
            _statusTimer.Tick += StatusTimerTick;

            OpenNewTab(string.IsNullOrEmpty(initialUrl) ? _homeUrl : initialUrl);
        }

        private BrowserTab ActiveTab
        {
            get
            {
                if (_activeIndex < 0 || _activeIndex >= _tabs.Count) return null;
                return _tabs[_activeIndex];
            }
        }

        private void InitializeUi()
        {
            SuspendLayout();

            Text = "IE 浏览器";
            AutoScaleMode = AutoScaleMode.None;
            Font = Ui.BaseFont;
            BackColor = Ui.Toolbar;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(Ui.Px(580), Ui.Px(420));
            Size = new Size(Ui.Px(1180), Ui.Px(760));
            KeyPreview = true;
            DoubleBuffered = true;
            TrySetIcon();

            _content = new Panel();
            _content.Dock = DockStyle.Fill;
            _content.BackColor = Color.White;
            _content.Margin = Padding.Empty;

            _progress = new ProgressLine();
            _progress.Dock = DockStyle.Fill;
            _progress.Margin = Padding.Empty;

            _tabBar = new TabStrip();
            _tabBar.Dock = DockStyle.Fill;
            _tabBar.Margin = Padding.Empty;
            _tabBar.TabSelected += OnTabBarSelected;
            _tabBar.TabCloseRequested += CloseTab;
            _tabBar.NewTabRequested += OnTabBarNewTab;

            _statusBar = new SimpleStatusBar();
            _statusBar.Dock = DockStyle.Fill;
            _statusBar.Margin = Padding.Empty;
            _statusBar.LeftText = "就绪";
            _statusBar.RightText = ActiveXPolicy.CompatibilityMode
                ? "兼容模式（IE7 内核）"
                : "IE" + BrowserFeatureControl.IeMajorVersion + " 内核";

            _toolbar = new ToolbarPanel();
            _toolbar.Dock = DockStyle.Fill;

            _backButton = CreateButton(GlyphIcon.Back, "后退 (Alt+←)", OnBackClick);
            _forwardButton = CreateButton(GlyphIcon.Forward, "前进 (Alt+→)", OnForwardClick);
            _refreshButton = CreateButton(GlyphIcon.Refresh, "刷新 (F5)", OnRefreshClick);
            _homeButton = CreateButton(GlyphIcon.Home, "主页", OnHomeClick);
            _starButton = CreateButton(GlyphIcon.Star, "添加到收藏夹 (Ctrl+D)", OnStarClick);
            _menuButton = CreateButton(GlyphIcon.Menu, "菜单", OnMenuClick);

            _addressBar = new AddressBar();
            _addressBar.Dock = DockStyle.Fill;
            _addressBar.NavigateRequested += OnAddressNavigate;
            _addressBar.CancelRequested += OnAddressCancel;

            _toolbar.Setup(_backButton, _forwardButton, _refreshButton, _homeButton,
                _addressBar, _starButton, _menuButton);

            var root = new BufferedTableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Margin = Padding.Empty;
            root.Padding = Padding.Empty;
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui.Px(48)));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui.Px(3)));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui.Px(36)));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui.Px(24)));

            root.Controls.Add(_toolbar, 0, 0);
            root.Controls.Add(_progress, 0, 1);
            root.Controls.Add(_tabBar, 0, 2);
            root.Controls.Add(_content, 0, 3);
            root.Controls.Add(_statusBar, 0, 4);

            Controls.Add(root);
            FormClosing += OnFormClosing;

            ResumeLayout(true);
        }

        private GlyphButton CreateButton(GlyphIcon icon, string tip, EventHandler click)
        {
            var button = new GlyphButton(icon);
            _toolTip.SetToolTip(button, tip);
            button.Click += click;
            return button;
        }

        private void TrySetIcon()
        {
            // 与 exe 图标同源：使用程序集内嵌的 app.ico（构建时同时写入 exe 资源）
            try
            {
                using (Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream("IEBrowser.app.ico"))
                {
                    if (stream != null)
                    {
                        _smallIcon = new Icon(stream, SystemInformation.SmallIconSize);
                        stream.Position = 0;
                        _largeIcon = new Icon(stream, SystemInformation.IconSize);
                        Icon = _largeIcon;
                        return;
                    }
                }
            }
            catch
            {
            }

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                return;
            }
            catch
            {
            }

            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    @"Internet Explorer\iexplore.exe");
                if (File.Exists(path))
                {
                    Icon = Icon.ExtractAssociatedIcon(path);
                }
            }
            catch
            {
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // 分别使用 16/32 像素的原始帧，标题栏与任务栏都保持清晰
            if (_smallIcon != null)
            {
                SendMessage(Handle, WmSetIcon, (IntPtr)IconSmall, _smallIcon.Handle);
            }
            if (_largeIcon != null)
            {
                SendMessage(Handle, WmSetIcon, (IntPtr)IconBig, _largeIcon.Handle);
            }
        }

        private void RestoreWindowBounds()
        {
            if (_settings.HasBounds && AppSettings.IsVisibleOnScreen(_settings.Bounds))
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = _settings.Bounds;
            }

            if (_settings.Maximized)
            {
                WindowState = FormWindowState.Maximized;
            }
        }

        // ---------- 标签页 ----------

        private void OpenNewTab(string url)
        {
            var tab = new BrowserTab();
            tab.Browser.Dock = DockStyle.Fill;
            tab.Browser.Visible = false;
            _content.Controls.Add(tab.Browser);
            AttachTab(tab);

            _tabs.Add(tab);
            _tabBar.AddTab(tab.Title);

            ActivateTab(_tabs.Count - 1);

            NavigateTab(tab, string.IsNullOrEmpty(url) ? _homeUrl : url);
        }

        private void AttachTab(BrowserTab tab)
        {
            tab.PopupRequested += OnPopupRequested;

            tab.Browser.DocumentTitleChanged += delegate { OnTabTitleChanged(tab); };

            tab.Browser.Navigated += delegate
            {
                OnTabNavigated(tab);
            };

            tab.Browser.DocumentCompleted += delegate
            {
                tab.IsLoading = false;
                OnTabNavigated(tab);
                if (IsActive(tab))
                {
                    _progress.Active = false;
                    _progress.Value = 0;
                }
                UpdateNavState();
            };

            tab.Browser.StatusTextChanged += delegate
            {
                if (IsActive(tab)) SetStatus(tab.Browser.StatusText);
            };

            tab.Browser.ProgressChanged += delegate(object sender, WebBrowserProgressChangedEventArgs e)
            {
                long percentage = -1;
                if (e.CurrentProgress >= 0 && e.MaximumProgress > 0)
                {
                    percentage = e.CurrentProgress * 100 / e.MaximumProgress;
                    if (percentage > 100) percentage = -1;
                }
                OnTabProgress(tab, percentage);
            };

            tab.Browser.CanGoBackChanged += delegate
            {
                if (IsActive(tab)) UpdateNavState();
            };

            tab.Browser.CanGoForwardChanged += delegate
            {
                if (IsActive(tab)) UpdateNavState();
            };

            tab.Browser.Navigating += delegate
            {
                tab.IsLoading = true;
                if (IsActive(tab))
                {
                    SetStatus("正在打开…");
                    _progress.Active = true;
                    if (_progress.Value < 6) _progress.Value = 6;
                }
                UpdateNavState();
            };
        }

        private void OnTabProgress(BrowserTab tab, long percentage)
        {
            if (!IsActive(tab)) return;

            if (percentage < 0)
            {
                tab.IsLoading = false;
                _progress.Active = false;
                _progress.Value = 0;
            }
            else
            {
                tab.IsLoading = true;
                int value = (int)percentage;
                if (value < 6) value = 6;
                if (value > 100) value = 100;
                _progress.Active = true;
                _progress.Value = value;
            }

            UpdateNavState();
        }

        private void OnTabTitleChanged(BrowserTab tab)
        {
            string title = string.Empty;
            try { title = tab.Browser.DocumentTitle; }
            catch { }

            if (string.IsNullOrEmpty(title))
            {
                title = string.IsNullOrEmpty(tab.Url) ? "新标签页" : ShortUrl(tab.Url);
            }

            tab.Title = title;

            int index = _tabs.IndexOf(tab);
            if (index >= 0) _tabBar.SetTitle(index, title);

            if (IsActive(tab)) UpdateWindowTitle();
        }

        private void OnTabNavigated(BrowserTab tab)
        {
            try
            {
                if (tab.Browser.Url != null) tab.Url = tab.Browser.Url.ToString();
            }
            catch
            {
            }

            if (!IsActive(tab)) return;

            SyncAddress();
            UpdateNavState();
            UpdateWindowTitle();
            AddHistory(tab.Url);
        }

        private void ActivateTab(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;

            _activeIndex = index;

            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].Browser.Visible = i == index;
            }

            _tabBar.SelectTab(index);

            try { _tabs[index].Browser.BringToFront(); }
            catch { }

            SyncAddress();
            UpdateNavState();
            UpdateWindowTitle();

            _progress.Active = _tabs[index].IsLoading;
            if (!_tabs[index].IsLoading) _progress.Value = 0;

            FocusBrowser();
        }

        private void CloseTab(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;

            BrowserTab closing = _tabs[index];
            BrowserTab active = ActiveTab;

            _tabs.RemoveAt(index);
            _tabBar.RemoveTab(index);

            try { _content.Controls.Remove(closing.Browser); }
            catch { }

            closing.Dispose();

            if (_tabs.Count == 0)
            {
                _activeIndex = -1;
                OpenNewTab(_homeUrl);
                return;
            }

            int next;
            if (active != null && active != closing)
            {
                next = _tabs.IndexOf(active);
            }
            else
            {
                next = Math.Min(index, _tabs.Count - 1);
            }

            _activeIndex = -1;
            ActivateTab(next);
        }

        private bool IsActive(BrowserTab tab)
        {
            return _tabs.IndexOf(tab) == _activeIndex;
        }

        private void OnTabBarSelected(int index)
        {
            ActivateTab(index);
        }

        private void OnTabBarNewTab()
        {
            OpenNewTab(_homeUrl);
        }

        private void OnPopupRequested(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            BeginInvoke((Action)delegate
            {
                OpenNewTab(url);
            });
        }

        private void FocusBrowser()
        {
            BrowserTab tab = ActiveTab;
            if (tab == null) return;
            try { tab.Browser.Focus(); }
            catch { }
        }

        private void NavigateActive(string input)
        {
            string url = ResolveInput(input);
            if (string.IsNullOrEmpty(url)) return;

            BrowserTab tab = ActiveTab;
            if (tab == null) return;

            NavigateTab(tab, url);
            FocusBrowser();
        }

        /// <summary>
        /// 所有导航的统一入口：命中危险旧版 ActiveX 的页面不交给内置 WebBrowser 渲染，
        /// 改为在独立的 32 位 iexplore.exe 中打开，避免控件本机崩溃拖垮本程序。
        /// </summary>
        private void NavigateTab(BrowserTab tab, string url)
        {
            if (tab == null || string.IsNullOrEmpty(url)) return;

            if (ActiveXPolicy.AutoIsolateActiveX)
            {
                string html = ActiveXIsolation.TryFetchHtml(url);
                if (ActiveXIsolation.HtmlContainsDangerousActiveX(html))
                {
                    string error;
                    if (ActiveXIsolation.OpenInIsolatedIe(url, out error))
                    {
                        SetStatus("该页面含旧版 ActiveX 控件，已在独立 IE 中打开（进程隔离）");
                        tab.Navigate(BuildIsolationPage(url));
                        return;
                    }

                    SetStatus("隔离打开失败：" + error);
                }
            }

            tab.Navigate(url);
        }

        /// <summary>
        /// 生成一个本地提示页，替代内嵌渲染该危险页面，告诉用户已外移。
        /// </summary>
        private static string BuildIsolationPage(string url)
        {
            string path = Path.Combine(
                Path.GetTempPath(), "iebrowser-isolated.html");

            string html =
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\">" +
                "<style>body{font-family:'Microsoft YaHei UI',sans-serif;padding:48px;" +
                "color:#333;background:#fafafa}h2{color:#444}" +
                "a{color:#0a66c2}code{background:#eee;padding:2px 6px;border-radius:3px}</style>" +
                "</head><body><h2>此页面已在独立 IE 窗口中打开</h2>" +
                "<p>该页面包含旧版 ActiveX 控件（griectl / Cognos ReportViewer），" +
                "为避免控件崩溃影响浏览器本体，已改用独立的 32 位 IE 进程加载。</p>" +
                "<p>页面地址：<br><code>" + System.Security.SecurityElement.Escape(url) + "</code></p>" +
                "<p>若独立窗口未出现，可 <a href=\"" +
                System.Security.SecurityElement.Escape(url) + "\">在此点击重新打开</a>。</p>" +
                "</body></html>";

            try
            {
                File.WriteAllText(path, html, System.Text.Encoding.UTF8);
                return new Uri(path).AbsoluteUri;
            }
            catch
            {
                return "about:blank";
            }
        }

        private static string ResolveInput(string input)
        {
            if (input == null) return null;

            string text = input.Trim();
            if (text.Length == 0) return null;

            if (SchemeRegex.IsMatch(text)) return text;
            if (text.StartsWith("\\\\")) return text;

            try
            {
                if (File.Exists(text) || Directory.Exists(text))
                {
                    return new Uri(Path.GetFullPath(text)).AbsoluteUri;
                }
            }
            catch
            {
            }

            if (LocalHostRegex.IsMatch(text)) return "http://" + text;
            if (DomainRegex.IsMatch(text)) return "http://" + text;

            if (!text.Contains(" ") && text.Contains(".")) return "http://" + text;

            return SearchUrl + Uri.EscapeDataString(text);
        }

        // ---------- 界面状态 ----------

        private void SyncAddress()
        {
            BrowserTab tab = ActiveTab;
            if (tab == null) return;
            if (_addressBar.ContainsAddressFocus) return;

            string url = tab.Url ?? string.Empty;
            if (string.Equals(url, _homeUrl, StringComparison.OrdinalIgnoreCase)) url = string.Empty;

            _addressBar.AddressText = url;
        }

        private void UpdateWindowTitle()
        {
            BrowserTab tab = ActiveTab;
            if (tab == null)
            {
                Text = "IE 浏览器";
                return;
            }

            Text = string.IsNullOrEmpty(tab.Title) ? "IE 浏览器" : tab.Title + " - IE 浏览器";
        }

        private void UpdateNavState()
        {
            BrowserTab tab = ActiveTab;
            bool hasTab = tab != null;

            bool canBack = false;
            bool canForward = false;

            if (hasTab)
            {
                try
                {
                    canBack = tab.Browser.CanGoBack;
                    canForward = tab.Browser.CanGoForward;
                }
                catch
                {
                }
            }

            _backButton.Enabled = canBack;
            _forwardButton.Enabled = canForward;
            _refreshButton.Icon = (hasTab && tab.IsLoading) ? GlyphIcon.Stop : GlyphIcon.Refresh;

            bool favorite = hasTab && _favorites.Contains(tab.Url);
            _starButton.Icon = favorite ? GlyphIcon.StarFilled : GlyphIcon.Star;
        }

        private void SetStatus(string text)
        {
            _statusBar.LeftText = string.IsNullOrEmpty(text) ? "就绪" : text;
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        private void StatusTimerTick(object sender, EventArgs e)
        {
            _statusTimer.Stop();
            _statusBar.LeftText = "就绪";
        }

        private void AddHistory(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return;
            if (_history.Contains(url)) return;

            _history.Insert(0, url);
            if (_history.Count > 80) _history.RemoveAt(_history.Count - 1);
            _addressBar.AddAutoComplete(url);
        }

        private static string ShortUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                return url;
            }
        }

        // ---------- 按钮 ----------

        private void OnBackClick(object sender, EventArgs e)
        {
            BrowserTab tab = ActiveTab;
            if (tab == null) return;
            try { if (tab.Browser.CanGoBack) tab.Browser.GoBack(); }
            catch { }
            UpdateNavState();
        }

        private void OnForwardClick(object sender, EventArgs e)
        {
            BrowserTab tab = ActiveTab;
            if (tab == null) return;
            try { if (tab.Browser.CanGoForward) tab.Browser.GoForward(); }
            catch { }
            UpdateNavState();
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            BrowserTab tab = ActiveTab;
            if (tab == null) return;

            try
            {
                if (tab.IsLoading)
                {
                    tab.Browser.Stop();
                    tab.IsLoading = false;
                    SetStatus("已停止加载");
                }
                else
                {
                    tab.Browser.Refresh();
                    SetStatus("正在刷新…");
                }
            }
            catch
            {
            }

            UpdateNavState();
        }

        private void OnHomeClick(object sender, EventArgs e)
        {
            BrowserTab tab = ActiveTab;
            if (tab != null) NavigateTab(tab, _homeUrl);
        }

        private void OnStarClick(object sender, EventArgs e)
        {
            ToggleFavorite();
        }

        private void ToggleFavorite()
        {
            BrowserTab tab = ActiveTab;
            if (tab == null || string.IsNullOrEmpty(tab.Url)) return;
            if (tab.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return;

            if (_favorites.Contains(tab.Url))
            {
                _favorites.Remove(tab.Url);
                SetStatus("已从收藏夹移除");
            }
            else
            {
                _favorites.Add(tab.Title, tab.Url);
                SetStatus("已添加到收藏夹");
            }

            _favorites.Save();
            UpdateNavState();
        }

        private void OnMenuClick(object sender, EventArgs e)
        {
            var menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.Font = Ui.BaseFont;

            var header = new ToolStripMenuItem("收藏夹");
            header.Enabled = false;
            menu.Items.Add(header);

            if (_favorites.Items.Count == 0)
            {
                var empty = new ToolStripMenuItem("（暂无收藏）");
                empty.Enabled = false;
                menu.Items.Add(empty);
            }
            else
            {
                foreach (FavoriteItem item in _favorites.Items)
                {
                    string url = item.Url;
                    menu.Items.Add(item.Title, null, delegate { OpenNewTab(url); });
                }
            }

            menu.Items.Add(new ToolStripSeparator());

            var newTabItem = new ToolStripMenuItem("新建标签页");
            newTabItem.ShortcutKeys = Keys.Control | Keys.T;
            newTabItem.Click += delegate { OpenNewTab(_homeUrl); };
            menu.Items.Add(newTabItem);

            menu.Items.Add("打开主页", null, delegate
            {
                BrowserTab tab = ActiveTab;
                if (tab != null) NavigateTab(tab, _homeUrl);
            });

            var refreshItem = new ToolStripMenuItem("刷新");
            refreshItem.ShortcutKeys = Keys.F5;
            refreshItem.Click += delegate { OnRefreshClick(null, EventArgs.Empty); };
            menu.Items.Add(refreshItem);

            menu.Items.Add(new ToolStripSeparator());

            var activeXMenu = new ToolStripMenuItem("ActiveX 控件");

            var allowActiveX = new ToolStripMenuItem();
            Action refreshAllowText = delegate
            {
                allowActiveX.Text = (ActiveXPolicy.AllowActiveX ? "✓ " : "○ ") + "允许 ActiveX 控件";
            };
            refreshAllowText();
            allowActiveX.Click += delegate
            {
                ToggleActiveXOption(ActiveXOption.ActiveX);
                refreshAllowText();
            };
            activeXMenu.DropDownItems.Add(allowActiveX);

            var scopeHint = new ToolStripMenuItem("含未签名、未标记安全的内部控件");
            scopeHint.Enabled = false;
            activeXMenu.DropDownItems.Add(scopeHint);

            var internetZone = new ToolStripMenuItem();
            Action refreshInternetText = delegate
            {
                internetZone.Text = (ActiveXPolicy.AllowOnInternet ? "✓ " : "○ ") +
                    "在 Internet 区域也放行（高风险）";
            };
            refreshInternetText();
            internetZone.Click += delegate
            {
                ToggleActiveXOption(ActiveXOption.Internet);
                refreshInternetText();
            };
            activeXMenu.DropDownItems.Add(internetZone);

            menu.Items.Add(activeXMenu);

            menu.Items.Add(new ToolStripSeparator());

            // 一级菜单：旧版控件兼容模式（最容易出问题的选项放在最外层）。
            // 注意：ContextMenuStrip 设置 ShowImageMargin=false 后不渲染 Checked 勾选，
            // 因此这里用文本前缀“✓ / ○”自绘状态，保证用户能看见是否已开启。
            var compatItem = new ToolStripMenuItem();
            Action refreshCompatText = delegate
            {
                compatItem.Text = (ActiveXPolicy.CompatibilityMode ? "✓ " : "○ ") +
                    "旧版控件兼容模式（IE7 内核，需重启）";
            };
            refreshCompatText();
            compatItem.Click += delegate
            {
                ActiveXPolicy.CompatibilityMode = !ActiveXPolicy.CompatibilityMode;
                SaveSettings();
                refreshCompatText();
                SetStatus("兼容模式已" +
                    (ActiveXPolicy.CompatibilityMode ? "开启" : "关闭") + "，重启程序后生效");
            };
            menu.Items.Add(compatItem);

            var compatHint = new ToolStripMenuItem("针对 griectl 等老控件的窗口回调崩溃");
            compatHint.Enabled = false;
            menu.Items.Add(compatHint);

            menu.Items.Add(new ToolStripSeparator());

            // 一级菜单：进程隔离（保底方案）
            var isolateItem = new ToolStripMenuItem("在系统 IE 中打开当前页（进程隔离）");
            isolateItem.Click += delegate { OpenInIsolatedIE(); };
            menu.Items.Add(isolateItem);

            var autoIsolateItem = new ToolStripMenuItem();
            Action refreshAutoIsolateText = delegate
            {
                autoIsolateItem.Text = (ActiveXPolicy.AutoIsolateActiveX ? "✓ " : "○ ") +
                    "自动隔离含旧版 ActiveX 的页面";
            };
            refreshAutoIsolateText();
            autoIsolateItem.Click += delegate
            {
                ActiveXPolicy.AutoIsolateActiveX = !ActiveXPolicy.AutoIsolateActiveX;
                SaveSettings();
                refreshAutoIsolateText();
                SetStatus("自动隔离已" + (ActiveXPolicy.AutoIsolateActiveX ? "开启" : "关闭") +
                    "，下次导航生效");
            };
            menu.Items.Add(autoIsolateItem);

            var isolateHint = new ToolStripMenuItem("崩溃只影响独立 IE 进程，不影响本程序");
            isolateHint.Enabled = false;
            menu.Items.Add(isolateHint);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Internet 选项…", null, delegate { OpenInternetOptions(); });

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("关于 IE 浏览器", null, delegate { ShowAbout(); });

            Size size = menu.GetPreferredSize(Size.Empty);
            Point anchor = _menuButton.PointToScreen(new Point(_menuButton.Width, _menuButton.Height + Ui.Px(2)));
            menu.Show(new Point(anchor.X - size.Width, anchor.Y));
        }

        private enum ActiveXOption
        {
            ActiveX,
            Internet
        }

        private void ToggleActiveXOption(ActiveXOption option)
        {
            switch (option)
            {
                case ActiveXOption.ActiveX:
                    ActiveXPolicy.AllowActiveX = !ActiveXPolicy.AllowActiveX;
                    SetStatus("ActiveX 控件：" + (ActiveXPolicy.AllowActiveX ? "已允许" : "已禁止") + "，刷新后生效");
                    break;

                case ActiveXOption.Internet:
                    ActiveXPolicy.AllowOnInternet = !ActiveXPolicy.AllowOnInternet;
                    SetStatus("Internet 区域放行：" + (ActiveXPolicy.AllowOnInternet ? "已开启（高风险）" : "已关闭"));
                    break;
            }

            SaveSettings();

            // 已打开的页面按新策略重新加载
            BrowserTab tab = ActiveTab;
            if (tab != null && !string.IsNullOrEmpty(tab.Url))
            {
                try { tab.Browser.Refresh(); }
                catch { }
            }
        }

        private void OpenInternetOptions()
        {
            try
            {
                Process.Start("rundll32.exe", "shell32.dll,Control_RunDLL inetcpl.cpl");
            }
            catch
            {
                SetStatus("无法打开 Internet 选项");
            }
        }

        /// <summary>
        /// 在独立的 iexplore.exe 进程中打开当前页面。
        /// 第三方 ActiveX 的本机崩溃（c000041d/c0000005）只会终止该进程，
        /// 不再波及本程序。旧版 OCX 在真正的 IE 宿主中兼容性最佳。
        /// </summary>
        private void OpenInIsolatedIE()
        {
            BrowserTab tab = ActiveTab;
            if (tab == null || string.IsNullOrEmpty(tab.Url)) return;
            if (tab.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("当前页面无需隔离");
                return;
            }

            string error;
            if (ActiveXIsolation.OpenInIsolatedIe(tab.Url, out error))
            {
                SetStatus("已在独立 IE 进程中打开");
            }
            else
            {
                SetStatus("无法启动系统 IE：" + error);
            }
        }

        private void SaveSettings()
        {
            try
            {
                Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;

                _settings.AllowActiveX = ActiveXPolicy.AllowActiveX;
                _settings.AllowOnInternet = ActiveXPolicy.AllowOnInternet;
                _settings.CompatibilityMode = ActiveXPolicy.CompatibilityMode;
                _settings.AutoIsolateActiveX = ActiveXPolicy.AutoIsolateActiveX;
                _settings.Save(bounds, WindowState == FormWindowState.Maximized);
            }
            catch
            {
            }
        }

        private void ShowAbout()
        {
            string text =
                "IE 内核浏览器" + Environment.NewLine +
                "运行环境：.NET Framework " + Environment.Version +
                "（" + (Environment.Is64BitProcess ? "64 位" : "32 位") + "进程）" + Environment.NewLine +
                "渲染内核：Internet Explorer " + BrowserFeatureControl.IeMajorVersion +
                " (FEATURE_BROWSER_EMULATION=" + (BrowserFeatureControl.IeMajorVersion >= 11 ? "11001" : "兼容模式") + ")" +
                Environment.NewLine +
                "ActiveX：" + (ActiveXPolicy.AllowActiveX ? "已启用" : "已停用") +
                (ActiveXPolicy.AllowOnInternet ? "，Internet 区域放行" : "，仅本机与内网") +
                Environment.NewLine + Environment.NewLine +
                "快捷键：" + Environment.NewLine +
                "Ctrl+T 新建标签页    Ctrl+W 关闭标签页" + Environment.NewLine +
                "Ctrl+L 定位地址栏    Ctrl+D 收藏当前页" + Environment.NewLine +
                "F5 刷新    Alt+← 后退    Alt+→ 前进";

            MessageBox.Show(this, text, "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnAddressNavigate(string text)
        {
            NavigateActive(text);
        }

        private void OnAddressCancel()
        {
            SyncAddress();
            FocusBrowser();
        }

        // ---------- 快捷键 ----------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.T:
                    OpenNewTab(_homeUrl);
                    return true;

                case Keys.Control | Keys.W:
                    CloseTab(_activeIndex);
                    return true;

                case Keys.Control | Keys.L:
                    _addressBar.FocusAndSelect();
                    return true;

                case Keys.Control | Keys.D:
                    ToggleFavorite();
                    return true;

                case Keys.F5:
                    OnRefreshClick(null, EventArgs.Empty);
                    return true;

                case Keys.Alt | Keys.Left:
                    OnBackClick(null, EventArgs.Empty);
                    return true;

                case Keys.Alt | Keys.Right:
                    OnForwardClick(null, EventArgs.Empty);
                    return true;

                case Keys.Control | Keys.Tab:
                    if (_tabs.Count > 1) ActivateTab((_activeIndex + 1) % _tabs.Count);
                    return true;

                case Keys.Control | Keys.Shift | Keys.Tab:
                    if (_tabs.Count > 1) ActivateTab((_activeIndex - 1 + _tabs.Count) % _tabs.Count);
                    return true;

                case Keys.Control | Keys.D1:
                case Keys.Control | Keys.D2:
                case Keys.Control | Keys.D3:
                case Keys.Control | Keys.D4:
                case Keys.Control | Keys.D5:
                case Keys.Control | Keys.D6:
                case Keys.Control | Keys.D7:
                case Keys.Control | Keys.D8:
                case Keys.Control | Keys.D9:
                    int index = (int)(keyData & Keys.KeyCode) - (int)Keys.D1;
                    if (index < _tabs.Count) ActivateTab(index);
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------- 关闭 ----------

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();

            foreach (BrowserTab tab in _tabs.ToArray())
            {
                tab.Dispose();
            }
            _tabs.Clear();
        }
    }
}
