using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace IEBrowser
{
    /// <summary>
    /// 让 WebBrowser 控件使用指定版本的 IE 内核（默认会被强制为 IE7 模式）。
    /// </summary>
    internal static class BrowserFeatureControl
    {
        private const string FeatureRoot = @"Software\Microsoft\Internet Explorer\Main\FeatureControl\";

        /// <summary>本机实际安装的 IE 主版本号（用于限制可选项上限）。</summary>
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
        /// 允许选择的内核版本（只列出本机 IE 实际支持的上限及以下版本）。
        /// WebBrowser 控件无法模拟比本机安装版本更高的内核。
        /// </summary>
        public static int[] SupportedVersions
        {
            get
            {
                int max = IeMajorVersion;
                var list = new System.Collections.Generic.List<int>();
                int[] candidates = { 11, 10, 9, 8, 7 };
                foreach (int v in candidates)
                {
                    if (v <= max) list.Add(v);
                }
                if (list.Count == 0) list.Add(7);
                return list.ToArray();
            }
        }

        /// <summary>
        /// 把用户选择的主版本号换算成 FEATURE_BROWSER_EMULATION 的值。
        /// </summary>
        /// <summary>供“关于”等界面展示当前版本对应的 emulation 数值。</summary>
        public static int EmulationForDisplay(int version)
        {
            return EmulationValue(version);
        }

        private static int EmulationValue(int version)
        {
            switch (version)
            {
                case 11: return 11001;  // IE11 强制标准模式（忽略 DOCTYPE）
                case 10: return 10001;  // IE10 标准模式
                case 9: return 9999;    // IE9 标准模式（亦可用 9000）
                case 8: return 8000;    // IE8 标准模式
                default: return IeMajorVersion >= 8 ? 7001 : 7000;  // IE7 模式
            }
        }

        /// <summary>
        /// 按指定内核版本写入 FeatureControl 注册表项。
        /// version 为主版本号（7/8/9/10/11），不合法时回退到本机最高版本。
        /// </summary>
        public static void Apply(int version)
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

            int[] supported = SupportedVersions;
            bool known = false;
            foreach (int v in supported)
            {
                if (v == version) { known = true; break; }
            }
            if (!known) version = supported[0];

            WriteDword("FEATURE_BROWSER_EMULATION", exeName, EmulationValue(version));

            // GPU 合成渲染在 IE9 以下不存在；IE9 及以上保持开启（griectl 崩溃的
            // 真因是 DEP/NXCOMPAT，与 GPU 渲染无关，详见 IEBrowser.csproj 注释）。
            WriteDword("FEATURE_GPU_RENDERING", exeName, version >= 9 ? 1 : 0);
            WriteDword("FEATURE_DISABLE_NAVIGATION_SOUNDS", exeName, 1);
            WriteDword("FEATURE_LOCALMACHINE_LOCKDOWN", exeName, 0);
        }

        /// <summary>按当前全局设置写入。</summary>
        public static void Apply()
        {
            Apply(ActiveXPolicy.IeVersion);
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
