// 窗口尺寸和位置坐标类型
using System.Drawing;
// JSON 反序列化（解析前端发送的消息）
using System.Text.Json;
// Photino 桌面窗口框架
using Photino.NET;
// 下载管理（下载源偏好：BMCLAPI 镜像开关）
using MinecraftLaunch;

namespace DeciLauncher;

partial class Program
{
    // ===== 拖拽状态 =====
    // 窗口 X 坐标追踪（用于 macOS/Linux 手动拖拽）
    private static double DragX = 0, DragY = 0;

    /// <summary>
    /// 前端就绪标记：收到页面发来的任意一条消息即置位（App.vue 挂载后会主动发多条）。
    /// 启动看门狗据此判断前端是否真的渲染出来了（见 Program.cs）——避免窗口全透明却毫无提示
    /// </summary>
    internal static volatile bool FrontendReady;

    /// <summary>
    /// 构建并配置 Photino 窗口（chromeless、无边框、DPI 自适应）
    /// </summary>
    private static PhotinoWindow BuildWindow(string appUrl, float scale, bool opaque)
    {
        var (width, height) = GetScaledSize(scale);

        var window = new PhotinoWindow()
            // 设置窗口标题
            .SetTitle("Deci Launcher")
            // 不使用操作系统默认位置
            .SetUseOsDefaultLocation(false)
            // 不使用操作系统默认尺寸
            .SetUseOsDefaultSize(false)
            // 设置窗口大小（DPI 缩放后）
            .SetSize(new Size(width, height))
            // 限制窗口最大宽度
            .SetMaxWidth(width)
            // 限制窗口最大高度
            .SetMaxHeight(height)
            // 初始位置放在屏幕外：WebView2 首帧之前是一块不透明黑底，先显示会看到黑屏闪现；
            // 前端首帧就绪后再由 Center() 移回屏幕中央（见消息处理器与 RegisterWindowCreatedHandler）
            .SetLocation(new Point(OffScreenX, OffScreenY))
            // 禁止用户手动调整窗口大小
            .SetResizable(false)
            // 禁止窗口最大化
            .SetMaximized(false)
            // 启用无边框模式（chromeless：隐藏 OS 原生标题栏）
            .SetChromeless(true)
            // 透明背景（opaque 回退模式下关闭：WebView2 透明合成异常时保证窗口可见）
            .SetTransparent(!opaque)
            // ===== 禁用不需要的 WebView2 功能以降低内存占用 =====
            // 禁用右键上下文菜单
            .SetContextMenuEnabled(false)
            // 禁用开发者工具（F12）
            .SetDevToolsEnabled(false)
            // 禁用通知 API
            .SetNotificationsEnabled(false)
            // 禁用摄像头/麦克风媒体流
            .SetMediaStreamEnabled(false)
            // 禁用媒体自动播放
            .SetMediaAutoplayEnabled(false)
            // 允许 JavaScript 剪贴板访问（复制 UUID 按钮依赖 navigator.clipboard.writeText，仍需用户手势）
            .SetJavascriptClipboardAccessEnabled(true)
            // 禁用文件系统访问 API
            .SetFileSystemAccessEnabled(false)
            // 禁用全屏模式
            .SetFullScreen(false)
            // WebView2 参数构建见 Launching/WebViewArgumentsBuilder（纯函数，含单测）
            .SetBrowserControlInitParameters(WebViewArgumentsBuilder.Build(scale))
            // ===== 注册窗口事件处理器 =====
            // 窗口创建完成后设置就绪标记
            .RegisterWindowCreatedHandler((sender, args) =>
            {
                // 兜底：链式 SetLocation 若未被底层采纳，这里再确保窗口在创建时就位于屏幕外，
                // 从而遮住 WebView2 首帧之前的不透明黑底
                ((PhotinoWindow)sender!).MoveTo(OffScreenX, OffScreenY, true);

                // Windows：补全系统菜单和最小化样式，启用任务栏点击最小化/恢复
                if (OperatingSystem.IsWindows())
                {
                    var hWnd = ((PhotinoWindow)sender!).WindowHandle;
                    var style = GetWindowLongPtr(hWnd, GWL_STYLE);
                    SetWindowLongPtr(hWnd, GWL_STYLE, style | (nint)(WS_SYSMENU | WS_MINIMIZEBOX));
                    SetWindowPos(hWnd, 0, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
                }
            })
            // 前端 Web 消息接收处理器
            .RegisterWebMessageReceivedHandler((object? sender, string message) =>
            {
                var window = (PhotinoWindow)sender!;
                // 任何一条入站消息都证明页面 JS 已挂载并渲染（启动看门狗据此判定前端可见）
                if (!FrontendReady)
                {
                    FrontendReady = true;
                    // 首帧已上屏（前端在双 rAF 之后才发消息），此时把窗口从屏幕外移回屏幕中央，
                    // 用户看到的第一眼就是渲染完成的 UI，不会看到 WebView2 首帧前的黑底
                    window.Invoke(() => window.Center());
                    Log.Info("[Window] 前端已就绪，窗口已显示");
                }

                try
                {
                    // 解析 JSON 消息
                    using var json = JsonDocument.Parse(message);
                    var root = json.RootElement;
                    // 读取消息类型字段
                    var type = root.GetProperty("type").GetString();

                    // ---- 拖拽开始 ----
                    if (type == "drag-start")
                    {
                        // Windows：使用原生系统拖拽（零延迟）
                        if (OperatingSystem.IsWindows())
                        {
                            // 获取原生窗口句柄
                            var hWnd = window.WindowHandle;
                            // 释放鼠标捕获
                            ReleaseCapture();
                            // 向窗口发送标题栏拖拽消息，触发系统级窗口移动
                            SendMessage(hWnd, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                        }
                        else
                        {
                            // macOS/Linux：记录窗口当前位置用于手动拖拽追踪
                            var pos = window.Location;
                            DragX = pos.X;
                            DragY = pos.Y;
                        }
                        return;
                    }

                    // ---- 拖拽中 ----
                    if (type == "drag")
                    {
                        // Windows 下原生拖拽由系统处理，忽略前端增量消息
                        if (OperatingSystem.IsWindows())
                            return;

                        // 读取鼠标位移增量
                        int dx = root.GetProperty("dx").GetInt32();
                        int dy = root.GetProperty("dy").GetInt32();
                        // 累加到窗口追踪位置
                        DragX += dx;
                        DragY += dy;
                        // 移动窗口到新位置（允许跨显示器）
                        window.MoveTo((int)Math.Round(DragX), (int)Math.Round(DragY), true);
                        return;
                    }

                    // ---- 获取应用信息（关于页：版本号取自程序集，与 csproj Version 单点同步） ----
                    if (type == "get-app-info")
                    {
                        // 版本号单一来源：Program.AppVersion（csproj <Version> → 程序集 InformationalVersion）
                        // 展示格式：1.0.0-beta.2 → 1.0.0 Beta 2
                        var version = AppVersion
                            .Replace("-beta.", " Beta ")
                            .Replace("-alpha.", " Alpha ");
                        TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "app-info", version }));
                        return;
                    }

                    // ---- 设置界面语言（国际化：前端启动/切换语言时同步给后端，用于错误消息） ----
                    if (type == "set-language")
                    {
                        var lang = root.TryGetProperty("language", out var l) ? l.GetString() : null;
                        if (lang is "zh-CN" or "en-US")
                            CurrentLanguage = lang;
                        return;
                    }

                    // ---- 设置下载源偏好（前端启动/切换时同步，控制库的 BMCLAPI 镜像开关） ----
                    if (type == "set-download-source")
                    {
                        var source = root.TryGetProperty("source", out var ds) ? ds.GetString() ?? "" : "";
                        // mirror：尽量使用镜像源（BMCLAPI CDN）
                        // official-first：优先官方源、加载缓慢时换镜像 —— 自动回退依赖下载功能
                        //   （待下载任务落地时实现超时/失败回退，现阶段等价于 official）
                        // official：尽量使用官方源
                        DownloadManager.IsEnableMirror = source == "mirror";
                        return;
                    }

                    // ---- 关闭窗口 ----
                    if (type == "close")
                    {
                        // 仅取消尚未创建进程的启动流程；已在运行的游戏有意保留：
                        // 关闭启动器不应中断玩家正在进行的游戏（HMCL/PCL/官方启动器同此约定）。
                        // 窗口销毁后启动流程的迟到回调由 TryNotifyWindow 兜底捕获，不会崩溃。
                        // 锁内取消，避免与启动线程的 CTS 重建交错（见 Program.Launch.cs CtsLock）
                        if (RunningProcess == null)
                        {
                            lock (CtsLock)
                                LaunchCts.Cancel();
                        }
                        window.Close();
                        return;
                    }

                    // ---- 最小化窗口 ----
                    if (type == "minimize")
                    {
                        window.SetMinimized(true);
                        return;
                    }

                    // ---- 扫描本机 Java 运行时 ----
                    if (type == "scan-java")
                    {
                        // force=true（用户手动刷新）时重新全盘扫描，否则复用后端缓存
                        var force = root.TryGetProperty("force", out var f) &&
                                    f.ValueKind == JsonValueKind.True;
                        _ = ScanJavaAsync(window, force);
                        return;
                    }

                    // ---- 扫描 .minecraft 目录下的游戏版本 ----
                    if (type == "scan-games")
                    {
                        var gamePath = root.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                        if (string.IsNullOrEmpty(gamePath))
                            gamePath = Path.Combine(AppContext.BaseDirectory, ".minecraft");
                        ScanGames(window, gamePath);
                        return;
                    }

                    // ---- 打开文件夹选择器选择游戏来源目录 ----
                    if (type == "pick-game-path")
                    {
                        _ = PickGamePathAsync(window);
                        return;
                    }

                    // ---- 创建离线账户 ----
                    if (type == "create-offline-account")
                    {
                        var name = root.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(name))
                            CreateOfflineAccount(window, name);
                        return;
                    }

                    // ---- 获取账户列表 ----
                    if (type == "list-accounts")
                    {
                        SendAccountList(window);
                        return;
                    }

                    // ---- 删除账户 ----
                    if (type == "delete-offline-account")
                    {
                        var deleteUuid = root.TryGetProperty("uuid", out var du) ? du.GetString() : null;
                        if (!string.IsNullOrEmpty(deleteUuid))
                            DeleteAccount(window, deleteUuid);
                        return;
                    }

                    // ---- 启动游戏 ----
                    if (type == "launch-game")
                    {
                        var gameId = root.TryGetProperty("gameId", out var gi) ? gi.GetString() ?? "" : "";
                        var accountUuid = root.TryGetProperty("accountUuid", out var au) ? au.GetString() ?? "" : "";
                        var javaPath = root.TryGetProperty("javaPath", out var jp) ? jp.GetString() ?? "" : "";
                        var maxMemory = root.TryGetProperty("maxMemory", out var mm) ? mm.GetInt32() : 2048;
                        var minecraftPath = root.TryGetProperty("minecraftPath", out var mp) ? mp.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(gameId) && !string.IsNullOrEmpty(accountUuid))
                            _ = LaunchGameAsync(window, gameId, accountUuid, javaPath, maxMemory, minecraftPath);
                        return;
                    }

                    // ---- 关闭游戏 ----
                    if (type == "close-game")
                    {
                        CloseGame(window);
                        return;
                    }

                    // ---- 取消启动 ----
                    if (type == "cancel-launch")
                    {
                        CancelLaunch(window);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"[Window] 消息解析失败: {ex.Message}");
                }
            })
            // 加载前端页面（load WebView2 content）
            .Load(appUrl);

        return window;
    }
}
