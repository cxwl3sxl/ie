# IE 内核浏览器（.NET Framework 4.0 + WinForms）

一个用 **C# / .NET Framework 4.0 / WinForms `WebBrowser` 控件** 实现的多标签浏览器，
渲染内核就是系统自带的 **Internet Explorer（Trident）**，并已通过
`FEATURE_BROWSER_EMULATION` 强制使用 IE11 标准模式（`documentMode = 11`）。

## 功能

- 多标签页：新建 / 切换 / 关闭（含中键关闭、双击空白处新建）
- 地址栏：智能识别网址 / 本地路径 / 搜索词，自动补全历史记录
- 导航：前进、后退、刷新、停止、主页
- 页面内 `window.open` 弹出窗口自动转为新标签页，不会跳出外部 IE 窗口
- 收藏夹（`Ctrl+D` 收藏 / 取消收藏，菜单中打开），持久化保存
- **ActiveX 控件支持**：通过自定义 `WebBrowser` 站点（`IServiceProvider` + `IInternetSecurityManager`）
  覆盖 URLACTION_ACTIVEX_* 策略，允许加载并运行 ActiveX 控件（含未签名、未标记安全的内部控件）。
  菜单 → "ActiveX 控件" 可切换。
- 加载进度条、状态栏显示链接目标与内核版本
- 记住窗口大小与位置
- 自绘界面：工具栏、标签条、地址栏、状态栏均为矢量绘制，支持高 DPI

## 快捷键

| 快捷键 | 功能 |
| --- | --- |
| `Ctrl + T` | 新建标签页 |
| `Ctrl + W` | 关闭当前标签页 |
| `Ctrl + L` | 定位到地址栏 |
| `Ctrl + D` | 收藏 / 取消收藏当前页 |
| `F5` | 刷新 |
| `Alt + ←` / `Alt + →` | 后退 / 前进 |
| `Ctrl + Tab` / `Ctrl + Shift + Tab` | 切换标签页 |
| `Ctrl + 1..9` | 跳到第 N 个标签页 |

## 编译

需要 .NET Framework 4.0 目标包（Visual Studio 2010 及以上版本自带；VS2022 需勾选
“.NET Framework 4 targeting pack”）。

```powershell
# 命令行编译
.\build.ps1

# 或者
msbuild IEBrowser.sln /p:Configuration=Release
```

输出：`IEBrowser\bin\Release\IEBrowser.exe`（无需安装，双击即可运行）。

## 目录结构

```
IEBrowser.sln
IEBrowser/
  Program.cs                  入口，设置 IE 内核特性、异常兜底
  MainForm.cs                 主窗口：标签管理、地址解析、快捷键、收藏夹菜单
  BrowserTab.cs               单个标签页的 WebBrowser 封装 + 弹窗拦截（COM 事件）
  ActiveXBrowser.cs            ActiveX 启用的 WebBrowser 子类 + 放行策略 + 安全站点
  ComInterop.cs                IServiceProvider / IInternetSecurityManager 互操作定义
  ComSink.cs                   DWebBrowserEvents2 / IConnectionPoint 互操作定义
  BrowserFeatureControl.cs    写入 FEATURE_BROWSER_EMULATION 等注册表项
  TabStrip.cs                 自绘标签条
  AddressBar.cs               自绘圆角地址栏（含提示文字）
  GlyphButton.cs              矢量图标按钮
  LayoutControls.cs           工具栏、进度条、状态栏布局控件
  FavoritesStore.cs           收藏夹 XML 读写
  AppSettings.cs              窗口位置记忆
  StartPage.cs                生成"新标签页"HTML
  Ui.cs                       配色、字体、DPI 与圆角绘制辅助
  app.ico                     程序图标（同时作为 exe 图标与窗口图标）
  app.manifest                高 DPI 感知 + 系统兼容性声明
```

## 说明

1. **IE 内核模式**：程序启动时会在
   `HKCU\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION`
   下为 `IEBrowser.exe` 写入 `11001`（IE11 强制标准模式）。
   该键按**可执行文件名**匹配，所以**不要重命名 exe**，否则会回退到 IE7 模式。
   同时写入 32/64 位两个注册表视图，AnyCPU 编译都能生效。
2. **IE11 依赖**：Windows 10/11 仍内置 Trident 引擎（IE11 模式），程序可直接运行；
   Windows 7/8 需安装 IE11 才能获得最佳效果。
3. 页面内容是 IE 内核渲染，对现代前端框架（React/Vue 新语法、CSS Grid 等）支持有限，
   这是内核本身的限制。
4. 收藏夹与配置保存在 `%AppData%\IEBrowser\`：
   `favorites.xml`、`settings.ini`、`start.html`。

## ActiveX 控件支持

浏览器通过自定义 `WebBrowser` 站点（`SecuritySite`）实现 ActiveX 放行：

- **`IServiceProvider.QueryService`** 对非安全管理器的服务返回 `INET_E_DEFAULT_ACTION`，
  交回 IE 默认处理（关键修复：返回自身接口会破坏 IE 导航事件）。
- **`IInternetSecurityManager.ProcessUrlAction`** 在控件创建前覆盖
  `UrlActionActiveXRun` (0x1200)、`UrlActionActiveXOverrideObjectSafety` (0x1201)、
  `UrlActionActiveXOverrideDataSafety` (0x1202)、`UrlActionActiveXOverrideScriptSafety` (0x1203)、
  `UrlActionActiveXConfirmNoUnsafe` (0x1204) 等动作的策略值。
- **区域门控**：放行规则默认仅对可信 URL（`file://`、`about:`、`res:`、`mk:`、
  `localhost`、`127.0.0.1`、`::1`、`.local` 域名、私有 IPv4、单标签主机名）生效。
  勾选"在 Internet 区域也放行（高风险）"后扩展到所有 URL。

### 菜单选项

| 菜单项 | 说明 |
| --- | --- |
| 允许 ActiveX 控件 | 主开关，默认开启。关闭后所有 ActiveX 控件均无法创建。 |
| 在 Internet 区域也放行（高风险） | 默认关闭。开启后对任意 URL 放行 ActiveX。 |

### 配置文件

`settings.ini` 中的对应项：

| 键 | 说明 |
| --- | --- |
| `afx` | ActiveX 主开关（`1` 开启 / `0` 关闭） |
| `afx_internet` | Internet 区域放行（`1` 开启 / `0` 关闭） |

编译目标为 `x86`，确保 32 位 ActiveX 控件能正常加载。
