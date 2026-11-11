using System;
using System.Runtime.InteropServices;

namespace IEBrowser
{
    [ComImport]
    [Guid("B196B284-BAB4-101A-B69C-00AA00341D07")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IConnectionPointContainer
    {
        void EnumConnectionPoints(out object ppEnum);
        void FindConnectionPoint(ref Guid riid, out IConnectionPoint ppCP);
    }

    [ComImport]
    [Guid("B196B286-BAB4-101A-B69C-00AA00341D07")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IConnectionPoint
    {
        void GetConnectionInterface(out Guid pIID);
        void GetConnectionPointContainer(out IConnectionPointContainer ppCPC);
        [PreserveSig]
        int Advise([MarshalAs(UnmanagedType.Interface)] object pUnkSink, out int pdwCookie);
        [PreserveSig]
        int Unadvise(int dwCookie);
        void EnumConnections(out object ppEnum);
    }

    /// <summary>
    /// DWebBrowserEvents2 的事件接收器（仅实现需要用到的 NewWindow3）。
    /// </summary>
    [ComVisible(true)]
    [ComImport]
    [Guid("34A715A0-6587-11D0-924A-0020AFC7AC4D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    internal interface DWebBrowserEvents2
    {
        [DispId(273)]
        void NewWindow3(
            [MarshalAs(UnmanagedType.IDispatch)] ref object ppDisp,
            [MarshalAs(UnmanagedType.VariantBool)] ref bool Cancel,
            uint dwFlags,
            [MarshalAs(UnmanagedType.BStr)] string bstrUrlContext,
            [MarshalAs(UnmanagedType.BStr)] string bstrUrl);
    }

    /// <summary>
    /// WebBrowser 站点向 MSHTML 提供的服务工厂（用于返回自定义 IInternetSecurityManager）。
    /// </summary>
    [ComImport]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IServiceProvider
    {
        [PreserveSig]
        int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
    }

    /// <summary>
    /// 站点安全策略接口：覆盖 URLACTION_ACTIVEX_* 等动作，决定控件是否可加载、运行。
    /// </summary>
    [ComImport]
    [Guid("79EAC9EE-BAF9-11CE-8C82-00AA004BA90B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IInternetSecurityManager
    {
        [PreserveSig]
        int SetSecuritySite(IntPtr site);

        [PreserveSig]
        int GetSecuritySite(out IntPtr site);

        [PreserveSig]
        int MapUrlToZone([MarshalAs(UnmanagedType.LPWStr)] string url, out int zone, int flags);

        [PreserveSig]
        int GetSecurityId([MarshalAs(UnmanagedType.LPWStr)] string url, IntPtr securityId,
            ref int cbSecurityId, IntPtr reserved);

        [PreserveSig]
        int ProcessUrlAction([MarshalAs(UnmanagedType.LPWStr)] string url, int action, IntPtr policy,
            int cbPolicy, IntPtr context, int cbContext, int flags, int reserved);

        [PreserveSig]
        int QueryCustomPolicy([MarshalAs(UnmanagedType.LPWStr)] string url, ref Guid key,
            out IntPtr policy, out int cbPolicy, IntPtr context, int cbContext, int reserved);

        [PreserveSig]
        int SetZoneMapping(int zone, [MarshalAs(UnmanagedType.LPWStr)] string pattern, int flags);

        [PreserveSig]
        int GetZoneMappings(int zone, out IntPtr enumString, int flags);
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    internal sealed class NewWindowSink : DWebBrowserEvents2
    {
        private readonly Action<string> _callback;

        public NewWindowSink(Action<string> callback)
        {
            _callback = callback;
        }

        public void NewWindow3(ref object ppDisp, ref bool Cancel, uint dwFlags, string bstrUrlContext, string bstrUrl)
        {
            Cancel = true;
            if (_callback != null && !string.IsNullOrEmpty(bstrUrl))
            {
                _callback(bstrUrl);
            }
        }
    }
}
