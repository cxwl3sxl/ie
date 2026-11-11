using System;
using System.IO;
using System.Text;

namespace IEBrowser
{
    /// <summary>
    /// 生成本地“新标签页”，并返回可导航的 file:// 地址。
    /// </summary>
    internal static class StartPage
    {
        private const string Html = @"<!DOCTYPE html>
<html>
<head>
<meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
<meta charset=""utf-8"" />
<title>新标签页</title>
<style>
html, body { height: 100%; margin: 0; overflow: hidden; }
body {
  font-family: ""Microsoft YaHei UI"", ""Segoe UI"", sans-serif;
  background: #f5f7fb;
  color: #1f2328;
  display: flex;
  align-items: center;
  justify-content: center;
}
.card { width: 560px; text-align: center; }
.logo { font-size: 62px; font-weight: 300; letter-spacing: 2px; color: #0f6cbd; }
.title { margin: 6px 0 0; font-size: 15px; color: #5b6470; }
form { margin: 34px 0 0; }
.box {
  display: flex;
  height: 48px;
  background: #ffffff;
  border: 1px solid #dfe3e8;
  border-radius: 24px;
  box-shadow: 0 8px 22px rgba(15, 108, 189, 0.08);
}
input[type=text] {
  flex: 1;
  border: 0;
  outline: none;
  background: transparent;
  font-family: inherit;
  font-size: 15px;
  padding: 0 22px;
  color: #1f2328;
}
button {
  width: 92px;
  border: 0;
  background: #0f6cbd;
  color: #ffffff;
  font-family: inherit;
  font-size: 15px;
  border-radius: 0 24px 24px 0;
  cursor: pointer;
}
.links { margin-top: 26px; font-size: 13px; color: #6b7280; }
.links a {
  display: inline-block;
  margin: 0 4px;
  color: #4b5563;
  text-decoration: none;
  padding: 6px 14px;
  border-radius: 14px;
}
.links a:hover { background: #e6eef9; color: #0f6cbd; }
.tip { margin-top: 46px; font-size: 12px; color: #9aa3af; }
</style>
</head>
<body>
<div class=""card"">
  <div class=""logo"">IE</div>
  <div class=""title"">Internet Explorer 内核浏览器</div>
  <form action=""https://www.bing.com/search"" method=""get"">
    <div class=""box"">
      <input type=""text"" name=""q"" placeholder=""搜索网页"" autofocus=""autofocus"" autocomplete=""off"" />
      <button type=""submit"">搜索</button>
    </div>
  </form>
  <div class=""links"">
    <a href=""https://www.bing.com"">Bing</a>
    <a href=""https://www.baidu.com"">百度</a>
    <a href=""https://www.microsoft.com"">微软</a>
    <a href=""https://github.com"">GitHub</a>
  </div>
  <div class=""tip"">Ctrl+T 新建标签页 &middot; Ctrl+L 定位地址栏 &middot; Ctrl+W 关闭标签页</div>
</div>
</body>
</html>";

        public static string Create()
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IEBrowser");
                Directory.CreateDirectory(folder);

                string file = Path.Combine(folder, "start.html");
                File.WriteAllText(file, Html, new UTF8Encoding(false));
                return new Uri(file).AbsoluteUri;
            }
            catch
            {
                return "about:blank";
            }
        }
    }
}
