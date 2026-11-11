using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace IEBrowser
{
    /// <summary>
    /// 让 WebBrowser 控件使用最高版本的 IE 内核（默认会被强制为 IE7 模式）。
    /// </summary>
    internal static class BrowserFeatureControl
    {
        private const string FeatureRoot = @"Software\Microsoft\Internet Explorer\Main\FeatureControl\";

        public static int IeMajorVersion
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Internet Explorer"))
                    {
                        if (key != null)
                        {
                            object value = key.GetValue("svcVersion") ?? key.GetValue("Version");
                            string text = value as string;
                            if (!string.IsNullOrEmpty(text))
                            {
                                int dot = text.IndexOf('.');
                                int major;
                                if (dot > 0 && int.TryParse(text.Substring(0, dot), out major))
                                {
                                    return major;
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
                return 11;
            }
        }

        /// <summary>
        /// compatibility 为 true 时启用旧版 ActiveX 兼容模式：
        /// 内核降回 IE7（7000/7001）、关闭 GPU 合成渲染。
        /// griectl 等老 OCX 在 IE11 模式 + GPU 渲染下会在 CreateWindowExW
        /// /窗口回调里触发本机访问违规（c000041d）。
        /// </summary>
        public static void Apply(bool compatibility)
        {
            string exeName;
            try
            {
                exeName = Path.GetFileName(Application.ExecutablePath);
            }
            catch
            {
                exeName = "IEBrowser.exe";
            }

            int major = IeMajorVersion;
            int emulation;
            if (compatibility)
            {
                // IE7 兼容：老 OCX 的目标宿主即 IE7
                emulation = major >= 8 ? 7001 : 7000;
            }
            else if (major >= 11) emulation = 11001;  // IE11 强制标准模式
            else if (major == 10) emulation = 10001;
            else if (major == 9) emulation = 9000;
            else if (major == 8) emulation = 8000;
            else emulation = 7000;

            WriteDword("FEATURE_BROWSER_EMULATION", exeName, emulation);
            WriteDword("FEATURE_GPU_RENDERING", exeName, compatibility ? 0 : 1);
            WriteDword("FEATURE_DISABLE_NAVIGATION_SOUNDS", exeName, 1);
            WriteDword("FEATURE_LOCALMACHINE_LOCKDOWN", exeName, 0);
        }

        public static void Apply()
        {
            Apply(ActiveXPolicy.CompatibilityMode);
        }

        private static void WriteDword(string feature, string exeName, int value)
        {
            WriteView(RegistryView.Registry64, feature, exeName, value);
            WriteView(RegistryView.Registry32, feature, exeName, value);
        }

        private static void WriteView(RegistryView view, string feature, string exeName, int value)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view))
                using (RegistryKey key = baseKey.CreateSubKey(FeatureRoot + feature))
                {
                    if (key != null)
                    {
                        key.SetValue(exeName, value, RegistryValueKind.DWord);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
