# AGENTS.md — IEBrowser (IE 内核浏览器)

## 项目概览

单项目 WinForms 桌面应用：C# / .NET Framework 4.0 / AnyCPU（实际编译为 x86），使用系统 `WebBrowser` 控件（Trident/IE11 内核）实现多标签浏览器。无测试项目、无 CI/CD、无 NuGet 依赖。

## 构建

```powershell
# 推荐：优先使用 VS 自带 MSBuild，回退到框架 SDK
.\build.ps1

# 或直接
msbuild IEBrowser.sln /p:Configuration=Release
```

- 输出：`IEBrowser\bin\Release\IEBrowser.exe`（无需安装，双击运行）
- Debug 构建输出到 `bin\Debug\`，配置常量 `DEBUG;TRACE`
- `obj/` 和 `bin/` 目录已包含构建产物，可安全忽略

## 项目结构

```
IEBrowser.sln              # VS 2010 解决方案
IEBrowser/
  Program.cs               # 入口：应用 BrowserFeatureControl → 启动exceptions → Run(MainForm)
  BrowserFeatureControl.cs # 向注册表写入 FEATURE_BROWSER_EMULATION 等（见下方关键约束）
  ComInterop.cs            # COM 互操作接口（DWebBrowserEvents2、IConnectionPoint 等）
  BrowserTab.cs            # 单标签页封装：WebBrowser + 弹窗拦截（COM Advise/Unadvise）
  ActiveXBrowser.cs        # 通过 IInternetSecurityManager 覆盖 ActiveX 策略
  LoginForm.cs             # 主窗口：标签管理、地址解析、快捷键、收藏夹菜单
  TabStrip.cs              # 自绘标签条（矢量图标、悬停/选中/关闭/新建）
  AddressBar.cs            # 自绘圆角地址栏
  GlyphButton.cs           # 矢量图标按钮（Segoe UI Symbol 字体）
  LayoutControls.cs        # 工具栏、进度条、状态栏
  Ui.cs                    # 全局 DPI 缩放、字体、颜色、圆角绘制辅助
  FavoritesStore.cs        # 收藏夹 XML（%AppData%\IEBrowser\favorites.xml）
  AppSettings.cs           # 窗口位置记忆（%AppData%\IEBrowser\settings.ini）
  StartPage.cs             # 生成"新标签页" HTML（%AppData%\IEBrowser\start.html）
  app.manifest             # 高 DPI 感知 + OS 兼容性声明
  app.ico                  # 程序图标
```

## 关键约束（容易踩坑）

1. **可执行文件名决定 IE 内核模式**：`BrowserFeatureControl.Apply()` 在 `HKCU\...\FeatureControl\FEATURE_BROWSER_EMULATION` 下以** exe 文件名**为键写入 `11001`（IE11 标准模式）。若重命名 exe，回退到 IE7 模式。**不要重命名 `IEBrowser.exe`**。

2. **平台目标为 x86**：虽然 `.csproj` 中 `Platform=AnyCPU`，但 Debug 和 Release 配置均显式设置 `<PlatformTarget>x86</PlatformTarget>`。生成的是 32 位进程。

3. **无测试、无 CI**：此项目没有测试项目、没有 CI 工作流、没有 pre-commit hooks。所有验证只能通过手动运行 exe 完成。

4. **字体依赖**：图标使用 `Segoe UI Symbol` 字体，中文使用 `Microsoft YaHei UI` 字体。两者在英文版 Windows 上可能缺失，代码有 fallback 逻辑但视觉效果会降级。

5. **注册表写入需要管理员权限？** 不，写入的是 `HKCU`（当前用户），不需要管理员。

6. **ActiveX 策略**：默认只对本机/内网页面放行 ActiveX，Internet 区域保持 IE 默认拦截。可在"工具"菜单中切换。

## 开发注意事项

- 项目使用 `System.Windows.Forms`（WinForms），所有 UI 控件均为自绘（`OnPaint` 重写），不使用标准主题。
- `AutoScaleMode = AutoScaleMode.None`：手动通过 `Ui.DpiScale` 处理高 DPI，不依赖系统自动缩放。
- COM 互操作（`ComInterop.cs`）涉及 `DWebBrowserEvents2` 事件接收器，修改时注意 `Advise`/`Unadvise` 的 cookie 管理和异常处理。
- 收藏夹和配置保存在 `%AppData%\IEBrowser\`，与用户数据目录相关，调试时注意清理。