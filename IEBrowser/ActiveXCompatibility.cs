using System;
using System.Runtime.InteropServices;

namespace IEBrowser
{
    /// <summary>
    /// 旧版 ActiveX 兼容辅助：去除窗口主题（Visual Styles）。
    /// griectl 等老 OCX 在启用主题的宿主里创建窗口（CreateWindowExW →
    /// GetClassLongW/GetDC 回调）时会触发本机访问违规（c000041d），
    /// SetWindowTheme(hwnd, "", "") 让控件按无主题路径绘制，可绕开该缺陷。
    /// </summary>
    internal static class ActiveXCompatibility
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

        /// <summary>
        /// 对控件及其全部子窗口去主题。需在句柄创建完成后调用。
        /// </summary>
        public static void StripThemeRecursive(IntPtr root)
        {
            if (root == IntPtr.Zero) return;

            ApplyToTree(root, 0);
        }

        private static void ApplyToTree(IntPtr handle, int depth)
        {
            if (depth > 16) return; // 防御异常深度的窗口树

            try
            {
                SetWindowTheme(handle, "", "");
            }
            catch
            {
            }

            try
            {
                IntPtr child = IntPtr.Zero;
                while ((child = FindWindowEx(handle, child, null, null)) != IntPtr.Zero)
                {
                    ApplyToTree(child, depth + 1);
                }
            }
            catch
            {
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(
            IntPtr parent, IntPtr after, string className, string windowName);
    }
}
