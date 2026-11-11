using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace IEBrowser
{
    /// <summary>
    /// ActiveX 放行策略，可在“工具”菜单中切换。
    /// 默认只对本机/内网页面生效，Internet 区域保持 IE 原有的拦截与提示。
    /// </summary>
    internal static class ActiveXPolicy
    {
        /// <summary>允许加载并运行 ActiveX 控件（含未签名、未标记安全的内部控件）。</summary>
        public static bool AllowActiveX = true;

        /// <summary>把上述放行规则同时应用到 Internet 区域，风险很高，默认关闭。</summary>
        public static bool AllowOnInternet = false;

        /// <summary>
        /// 是否向 WebBrowser 注入自定义 IInternetSecurityManager 站点。默认开启。
        /// 设为 false 时回退到 IE 默认安全站点，用于排查自定义站点引发的本机崩溃。
        /// </summary>
        public static bool UseCustomSecurityManager = true;

        /// <summary>
        /// 渲染内核版本（7/8/9/10/11），对应 FEATURE_BROWSER_EMULATION。
        /// 由菜单“IE 内核”切换，写入 settings.ini 的 ie_version 键，重启后生效。
        /// 注意：WebBrowser 控件无法模拟比本机安装的 IE 更高的版本。
        /// </summary>
        public static int IeVersion = 11;
    }

    /// <summary>
    /// 支持 ActiveX 的 WebBrowser：通过站点提供的 IInternetSecurityManager
    /// 覆盖 URLACTION_ACTIVEX_* 动作，避免控件被区域安全策略静默拦截。
    /// </summary>
    internal sealed class ActiveXBrowser : WebBrowser
    {
        private const int UrlActionDownloadSignedActiveX = 0x1001;
        private const int UrlActionDownloadUnsignedActiveX = 0x1004;

        private const int UrlActionActiveXRun = 0x1200;
        private const int UrlActionActiveXOverrideObjectSafety = 0x1201;
        private const int UrlActionActiveXOverrideDataSafety = 0x1202;
        private const int UrlActionActiveXOverrideScriptSafety = 0x1203;
        private const int UrlActionActiveXConfirmNoUnsafe = 0x1204;

        private const int UrlPolicyAllow = 0;
        private const int UrlPolicyDisallow = 3;

        private const int SOk = 0;
        private const int InetEDefaultAction = unchecked((int)0x800C0011);

        public ActiveXBrowser()
        {
        }

        protected override WebBrowserSiteBase CreateWebBrowserSiteBase()
        {
            if (ActiveXPolicy.UseCustomSecurityManager)
            {
                return new SecuritySite(this);
            }

            // 排查用：注入自定义安全站点时若发生本机崩溃（c0000005），
            // 将本开关置 false 可回退到 IE 默认站点以确认是否为站点引发。
            return base.CreateWebBrowserSiteBase();
        }

        /// <summary>
        /// 判断该 URL 是否属于本机/内网等“可信”范围。
        /// </summary>
        internal static bool IsTrustedUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return true;

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;

            switch (uri.Scheme.ToLowerInvariant())
            {
                case "file":
                case "about":
                case "res":
                case "mk":
                    return true;
                case "http":
                case "https":
                case "ftp":
                    break;
                default:
                    return false;
            }

            string host = uri.Host;
            if (string.IsNullOrEmpty(host)) return false;

            host = host.ToLowerInvariant();
            if (host == "localhost" || host == "127.0.0.1" || host == "::1") return true;
            if (host.EndsWith(".local", StringComparison.Ordinal)) return true;

            System.Net.IPAddress address;
            if (System.Net.IPAddress.TryParse(host, out address))
            {
                if (System.Net.IPAddress.IsLoopback(address)) return true;

                byte[] bytes = address.GetAddressBytes();
                if (bytes.Length == 4)
                {
                    // 10.0.0.0/8、172.16.0.0/12、192.168.0.0/16、169.254.0.0/16
                    if (bytes[0] == 10) return true;
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                    if (bytes[0] == 192 && bytes[1] == 168) return true;
                    if (bytes[0] == 169 && bytes[1] == 254) return true;
                }
                return false;
            }

            // 单标签主机名（如 http://oa/）视为内网。
            if (host.IndexOf('.') < 0) return true;

            return false;
        }

        internal static bool TryGetPolicy(string url, int action, out bool allow)
        {
            allow = false;

            switch (action)
            {
                case UrlActionActiveXRun:
                case UrlActionDownloadSignedActiveX:
                case UrlActionDownloadUnsignedActiveX:
                case UrlActionActiveXOverrideObjectSafety:
                case UrlActionActiveXOverrideDataSafety:
                case UrlActionActiveXOverrideScriptSafety:
                case UrlActionActiveXConfirmNoUnsafe:
                    // IE 在创建控件前会先查询 0x1201~0x1204，任何一个被拒绝都会导致
                    // 控件无法实例化，因此这些动作统一跟随主开关。
                    allow = ActiveXPolicy.AllowActiveX;
                    return true;

                default:
                    return false;
            }
        }

        [ComVisible(true)]
        [ClassInterface(ClassInterfaceType.None)]
        private sealed class SecuritySite : WebBrowserSite, IServiceProvider, IInternetSecurityManager
        {
            private IntPtr _managerSite = IntPtr.Zero;

            public SecuritySite(WebBrowser host)
                : base(host)
            {
            }

            // 以下所有方法都被本机 mshtml.dll / ActiveX 控件直接回调（UI 线程，但位于
            // WinForms Application.ThreadException 捕获范围之外）。一旦有托管异常越过 COM
            // 边界，运行时会破坏本机栈并直接终止进程（无异常对话框）。因此每个回调都必须
            // 自行吞掉异常，返回安全的 HRESULT。

            int IServiceProvider.QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject)
            {
                ppvObject = IntPtr.Zero;
                try
                {
                    // IInternetSecurityManager 的 service GUID 与其接口 IID 相同。
                    // QueryService 语义：guidService 指定服务、riid 指定想要的接口，
                    // 两者都必须匹配安全管理器，否则返回 INET_E_DEFAULT_ACTION，
                    // 让本机 IE 走默认处理。若放宽到只判断 guidService，会在调用方
                    // 实际想要其它接口时把安全管理器 vtable 交出去，造成 vtable 错配
                    // 与访问违规（c0000005）级崩溃。
                    Guid securityManager = typeof(IInternetSecurityManager).GUID;
                    if (guidService == securityManager && riid == securityManager)
                    {
                        ppvObject = Marshal.GetComInterfaceForObject(this, typeof(IInternetSecurityManager));
                        return SOk;
                    }
                }
                catch
                {
                    ppvObject = IntPtr.Zero;
                }

                // 其余服务交回 IE 默认处理。不能在这里返回站点自身的其它接口，
                // 否则 MSHTML 会拿到不属于该服务的对象，导致第二次导航不再触发事件。
                return InetEDefaultAction;
            }

            int IInternetSecurityManager.SetSecuritySite(IntPtr site)
            {
                // 保存站点指针时必须自行 AddRef，否则调用方释放后我方会持有悬空指针。
                try
                {
                    if (_managerSite != IntPtr.Zero) Marshal.Release(_managerSite);
                    _managerSite = site;
                    if (_managerSite != IntPtr.Zero) Marshal.AddRef(_managerSite);
                }
                catch
                {
                }
                return SOk;
            }

            int IInternetSecurityManager.GetSecuritySite(out IntPtr site)
            {
                // 返回给调用方的引用由调用方负责释放。
                site = IntPtr.Zero;
                try
                {
                    site = _managerSite;
                    if (site != IntPtr.Zero) Marshal.AddRef(site);
                }
                catch
                {
                    site = IntPtr.Zero;
                }
                return SOk;
            }

            int IInternetSecurityManager.MapUrlToZone(string url, out int zone, int flags)
            {
                zone = 0;
                return InetEDefaultAction;
            }

            int IInternetSecurityManager.GetSecurityId(string url, IntPtr securityId, ref int cbSecurityId,
                IntPtr reserved)
            {
                return InetEDefaultAction;
            }

            int IInternetSecurityManager.ProcessUrlAction(string url, int action, IntPtr policy, int cbPolicy,
                IntPtr context, int cbContext, int flags, int reserved)
            {
                try
                {
                    if (policy == IntPtr.Zero || cbPolicy < 4) return InetEDefaultAction;

                    bool allow;
                    if (!TryGetPolicy(url, action, out allow)) return InetEDefaultAction;

                    // 只接管本机/内网页面；Internet 区域默认仍按 IE 原策略处理。
                    if (!IsTrustedUrl(url) && !ActiveXPolicy.AllowOnInternet) return InetEDefaultAction;

                    Marshal.WriteInt32(policy, allow ? UrlPolicyAllow : UrlPolicyDisallow);
                    return SOk;
                }
                catch
                {
                    return InetEDefaultAction;
                }
            }

            int IInternetSecurityManager.QueryCustomPolicy(string url, ref Guid key, out IntPtr policy,
                out int cbPolicy, IntPtr context, int cbContext, int reserved)
            {
                policy = IntPtr.Zero;
                cbPolicy = 0;
                return InetEDefaultAction;
            }

            int IInternetSecurityManager.SetZoneMapping(int zone, string pattern, int flags)
            {
                return InetEDefaultAction;
            }

            int IInternetSecurityManager.GetZoneMappings(int zone, out IntPtr enumString, int flags)
            {
                enumString = IntPtr.Zero;
                return InetEDefaultAction;
            }
        }
    }
}
