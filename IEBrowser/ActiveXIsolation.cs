using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace IEBrowser
{
    /// <summary>
    /// 对“会拖垮本进程的旧版 ActiveX 页面”做进程隔离：
    /// 先在后台预取页面 HTML，命中危险 CLSID 时不交给内置 WebBrowser 渲染，
    /// 而是用独立的 32 位 iexplore.exe 打开，避免本程序被控件本机崩溃（c000041d）拖死。
    /// </summary>
    internal static class ActiveXIsolation
    {
        /// <summary>
        /// 已知会在本进程内触发本机访问违规的控件 CLSID（小写、带大括号）。
        /// Cognos GRID ReportViewer：griectl.dll 5.6.11.1118。
        /// </summary>
        private static readonly string[] DangerousClsids =
        {
            "{9e4cca44-17fc-402b-822c-bfa6cba77c0c}"
        };

        private static readonly Regex ObjectTagRegex = new Regex(
            @"<object\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ClassIdRegex = new Regex(
            @"classid\s*=\s*[""']?\s*(?:clsid:)?\s*(\{[0-9a-fA-F\-]+\})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// 判断给定 HTML 是否引用了危险 ActiveX 控件。
        /// </summary>
        public static bool HtmlContainsDangerousActiveX(string html)
        {
            if (string.IsNullOrEmpty(html)) return false;

            foreach (Match obj in ObjectTagRegex.Matches(html))
            {
                Match cls = ClassIdRegex.Match(obj.Value);
                if (!cls.Success) continue;

                string clsids = cls.Groups[1].Value.ToLowerInvariant();
                foreach (string bad in DangerousClsids)
                {
                    if (string.Equals(clsids, bad, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 预取页面 HTML（不执行脚本、不实例化控件），失败返回 null。
        /// 仅用于本机/内网 http(s) 页面。
        /// </summary>
        public static string TryFetchHtml(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.Timeout = 4000;
                request.ReadWriteTimeout = 4000;
                request.AllowAutoRedirect = true;
                request.UserAgent =
                    "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.1)";
                // 不自动跟随到别的凭据域也无妨；本机/内网通常无鉴权。
                request.Credentials = CredentialCache.DefaultCredentials;
                request.Proxy = WebRequest.DefaultWebProxy;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    // 只读前 512KB，足够判断 body 内是否含 <object>。
                    var buffer = new char[512 * 1024];
                    int read = reader.ReadBlock(buffer, 0, buffer.Length);
                    return new string(buffer, 0, read);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 用独立的 32 位 iexplore.exe 打开 URL（不阻塞调用方）。
        /// </summary>
        public static bool OpenInIsolatedIe(string url, out string error)
        {
            error = null;
            try
            {
                string exe = FindIexplore();

                if (string.IsNullOrEmpty(exe))
                {
                    // 找不到 IE 时退回系统默认浏览器，至少用户能看到页面。
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    return true;
                }

                // 显式使用 32 位 IE（SysWOW64），与 32 位 griectl 位数匹配。
                Process.Start(new ProcessStartInfo(exe, "\"" + url + "\"")
                {
                    UseShellExecute = false
                });
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 优先返回 32 位 iexplore.exe（SysWOW64），以保证能加载 32 位 OCX。
        /// </summary>
        private static string FindIexplore()
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            // 32 位 IE：C:\Windows\SysWOW64\iexplore.exe（64 位系统上）
            string wow = Path.Combine(windows, "SysWOW64", "iexplore.exe");
            if (File.Exists(wow)) return wow;

            // 32 位系统：C:\Windows\System32\iexplore.exe 本身就是 32 位
            string sys = Path.Combine(windows, "System32", "iexplore.exe");
            if (File.Exists(sys)) return sys;

            string pf = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Internet Explorer", "iexplore.exe");
            if (File.Exists(pf)) return pf;

            return null;
        }
    }
}
