using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IEBrowser
{
    /// <summary>
    /// 会话持久化：%AppData%\IEBrowser\session.txt。
    /// 每行一个标签页地址，第一行前缀 "active=N" 记录活动标签索引。
    /// 程序正常退出时写入，启动时读取以恢复上次会话。
    /// </summary>
    internal static class SessionStore
    {
        private static string FilePath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IEBrowser");
                return Path.Combine(folder, "session.txt");
            }
        }

        /// <summary>保存当前会话。urls 为空或全空时删除文件（下次启动空白启动）。</summary>
        public static void Save(IList<string> urls, int activeIndex)
        {
            try
            {
                if (urls == null || urls.Count == 0)
                {
                    if (File.Exists(FilePath)) File.Delete(FilePath);
                    return;
                }

                var sb = new StringBuilder();
                sb.Append("active=").Append(Math.Max(0, Math.Min(activeIndex, urls.Count - 1)))
                  .AppendLine();

                foreach (string url in urls)
                {
                    // 单行编码：地址内不会出现换行；空地址跳过
                    string u = (url ?? string.Empty).Trim();
                    if (u.Length == 0) continue;
                    sb.AppendLine(u);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        /// <summary>读取上次会话。没有可恢复内容时返回 null。</summary>
        public static SessionData Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;

                string[] lines = File.ReadAllLines(FilePath);
                int active = 0;
                var urls = new List<string>();

                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;

                    if (line.StartsWith("active=", StringComparison.OrdinalIgnoreCase))
                    {
                        int n;
                        if (int.TryParse(line.Substring(7), out n) && n >= 0) active = n;
                        continue;
                    }

                    urls.Add(line);
                }

                if (urls.Count == 0) return null;
                if (active >= urls.Count) active = urls.Count - 1;

                return new SessionData { Urls = urls.ToArray(), ActiveIndex = active };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>标记"本次启动应恢复会话"的开关文件（重启场景）。</summary>
        public static bool ConsumeRestartFlag()
        {
            try
            {
                string flag = FlagPath;
                if (!File.Exists(flag)) return false;
                File.Delete(flag);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void MarkRestart()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FlagPath));
                File.WriteAllText(FlagPath, "1");
            }
            catch
            {
            }
        }

        private static string FlagPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IEBrowser", "restart.flag");
            }
        }
    }

    internal sealed class SessionData
    {
        public string[] Urls { get; set; }
        public int ActiveIndex { get; set; }
    }
}
