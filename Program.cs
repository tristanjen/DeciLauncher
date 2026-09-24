// 反射（获取入口程序集以读取内嵌资源）
using System.Reflection;
// ASP.NET Core 最小化 API（Release 模式提供内嵌静态文件）
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
// 访问控制中间件（CookieOptions / StatusCodes / SameSiteMode 均在 Microsoft.AspNetCore.Http）
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
// 日志记录（仅 DEBUG 模式启用）
using Microsoft.Extensions.Logging;
// Minecraft 启动核心库（初始化下载线程、重试策略等）
using MinecraftLaunch;

namespace DeciLauncher;

partial class Program
{
    // 编译时常量：DEBUG 模式下为 true，RELEASE 模式下为 false
#if DEBUG
    public static readonly bool IsDebugMode = true;
#else
    public static readonly bool IsDebugMode = false;
#endif

    // ===== 语言状态（前端 set-language 消息同步） =====
    // 当前界面语言：默认 zh-CN，前端启动时按 localStorage/系统语言发送 set-language 覆盖
    internal static string CurrentLanguage = "zh-CN";
    // 双语消息辅助：按当前语言选择文案
    internal static string L(string zh, string en) => CurrentLanguage == "zh-CN" ? zh : en;
    // 致命错误文案：窗口加载前无前端语言信息，按系统 UI 语言选择
    internal static string FL(string zh, string en) =>
        System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? zh : en;

    // ===== 应用入口点 =====

    // STAThread：Windows COM 互操作要求（WebView2 底层依赖）
    [STAThread]
    static void Main(string[] args)
    {
        // 单实例保护：防止两个启动器实例同时管理同一 .minecraft / 并发启动游戏
        if (!AcquireSingleInstanceLock())
        {
            ShowFatalError(FL(
                "Deci Launcher 已经在运行。",
                "Deci Launcher is already running."));
            return;
        }

        // 文件日志初始化（Debug/Release 均启用；滚动保留 3 份历史，用户侧问题可追溯）
        Log.InitializeFile();

        // 全局异常兜底：OutputType 为 WinExe（无控制台），启动期未处理异常会让进程
        // 静默终止——用户只会看到"什么都没发生"或一个未渲染的透明窗口。
        // 统一转成文件日志 + 原生错误对话框，保证任何失败都可见、可追溯
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ShowFatalError(FL(
                $"启动器发生未处理异常：\n\n{e.ExceptionObject}",
                $"Unhandled launcher error:\n\n{e.ExceptionObject}"));
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            Log.Warn($"[FATAL] 未观察的任务异常: {e.Exception}");
        };

        try
        {
            Run(args);
        }
        catch (Exception ex)
        {
            Log.Warn($"[FATAL] 启动流程异常: {ex}");
            ShowFatalError(FL($"启动失败：\n\n{ex.Message}", $"Startup failed:\n\n{ex.Message}"));
        }
    }

    /// <summary>
    /// 实际启动流程（由 Main 统一包裹全局异常兜底，任何异常都会变成日志 + 对话框）
    /// </summary>
    private static void Run(string[] args)
    {
        // Windows 下解除 DPI 虚拟化以获取真实系统缩放值
        if (OperatingSystem.IsWindows())
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_SYSTEM_AWARE);

        // MinecraftLaunch 全局初始化（下载线程、重试、UserAgent 等）
        // MaxThread/MaxFragment 调低：启动器下载任务少，降低线程池/分片缓冲的常驻资源
        InitializeHelper.Initialize(settings =>
        {
            settings.MaxThread = 64;
            settings.MaxFragment = 32;
            settings.MaxRetryCount = 4;
            // 下载源默认「优先官方源」：BMCLAPI 镜像开关默认关闭，
            // 前端启动后按用户偏好（set-download-source 消息）覆盖；
            // 默认值与前端 stores/downloadSource.ts 的 DEFAULT_SOURCE 保持一致
            settings.IsEnableMirror = false;
            settings.IsEnableFragment = false;
            // 版本号单一来源：csproj <Version> → 程序集 InformationalVersion（+commitHash 已截断）
            settings.UserAgent = $"DeciLauncher/{AppVersion}";
        });

        // 从 Config/accounts.json 加载已保存的账户
        InitializeAccounts();

        // Release 模式：用 ManifestEmbeddedFileProvider 从 DLL 内嵌资源提供前端文件
        // 不创建物理 wwwroot 目录
        string appUrl;
        if (IsDebugMode)
        {
            // 探测 Vite 开发服务器：不仅要求可达，还要求响应内容确实是本项目的页面。
            // 否则 5173 被其它程序/陈旧 dev server 占用时会把别人的页面装进透明窗口——
            // 若那个页面没有铺满不透明背景，用户看到的就是一个"完全透明"的窗口
            if (!TryResolveDevServerUrl(out var devUrl))
            {
                ShowFatalError(FL(
                    "无法连接本项目的前端开发服务器（http://127.0.0.1:5173 或 http://localhost:5173）。\n\n" +
                    "可能原因：\n" +
                    "  1) 尚未在 UserInterface 目录运行 pnpm dev；\n" +
                    "  2) 5173 端口被其它程序占用（探测到的不是本项目页面）。\n\n" +
                    "请释放该端口或启动 pnpm dev 后重试。",
                    "Cannot reach this project's frontend dev server (http://127.0.0.1:5173 or http://localhost:5173).\n\n" +
                    "Possible causes:\n" +
                    "  1) pnpm dev has not been started in the UserInterface directory;\n" +
                    "  2) port 5173 is occupied by another program (the page served is not this project).\n\n" +
                    "Free the port or start pnpm dev, then retry."));
                return;
            }
            Log.Info($"[Dev] 前端开发服务器: {devUrl}");
            appUrl = devUrl;
        }
        else
        {
            // Slim builder：本启动器只用 Kestrel 托管内嵌静态文件 + 一个安全中间件，
            // 不需要完整 ASP.NET 的配置源/日志/服务注册，减少默认设施的内存与启动开销
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                Args = args,
                WebRootPath = AppContext.BaseDirectory
            });
            builder.Logging.ClearProviders();
            var assembly = Assembly.GetEntryAssembly();
            if (assembly != null)
            {
                var embeddedProvider = new ManifestEmbeddedFileProvider(
                    assembly, "Resources/wwwroot");
                builder.Environment.WebRootFileProvider = embeddedProvider;
            }

            // 安全：仅绑定 IPv4 回环地址，默认随机端口（0 = 由 Kestrel 自动分配），
            // 防止此前固定 8000 端口被同机其他程序/浏览器直接访问。
            // DECILAUNCHER_PORT / DECILAUNCHER_TOKEN 环境变量仅在 CI/DEBUG 下生效（供测试与诊断）
            var port = ReadConfiguredPort();
            var accessToken = ReadConfiguredToken();
            // 会话 cookie 值独立于导航 token：即使 URL token 泄露也无法冒充后续会话凭据
            var sessionToken = Guid.NewGuid().ToString("N");

            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
            var app = builder.Build();

            // 访问控制中间件：首次导航必须携带 token（query 参数），校验通过后
            // 下发 HttpOnly 会话 cookie（独立随机值），后续静态资源请求凭 cookie 通过；
            // 无凭据请求一律 404，阻止本机任意程序直接访问前端。
            // Referrer-Policy: no-referrer 防止 token 随 Referer 头泄露到任何外部站点
            app.Use(async (context, next) =>
            {
                var token = context.Request.Query["token"].ToString();
                if (token.Length > 0 && TokensEqual(token, accessToken))
                {
                    context.Response.Cookies.Append(AccessCookieName, sessionToken, new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict
                    });
                    context.Response.Headers["Referrer-Policy"] = "no-referrer";
                    await next();
                    return;
                }
                var cookie = context.Request.Cookies[AccessCookieName];
                if (!string.IsNullOrEmpty(cookie) && TokensEqual(cookie, sessionToken))
                {
                    context.Response.Headers["Referrer-Policy"] = "no-referrer";
                    await next();
                    return;
                }
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            });
            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions { DefaultContentType = "text/plain" });
            try
            {
                // StartAsync 同步等待服务器绑定完成，端口占用等错误在此抛出
                app.StartAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warn($"[Server] Web 服务器启动失败: {ex}");
                ShowFatalError(FL(
                    $"无法在 127.0.0.1:{port} 启动本地服务器，请确认端口未被占用。\n\n{ex.Message}",
                    $"Failed to start the local server on 127.0.0.1:{port}. Make sure the port is not in use.\n\n{ex.Message}"));
                return;
            }

            // 随机端口模式下从 Kestrel 读取实际绑定地址（port=0 时此处为已分配的真实端口）
            var boundUrl = app.Urls.FirstOrDefault(u =>
                u.StartsWith("http://127.0.0.1", StringComparison.Ordinal));
            if (boundUrl == null)
            {
                ShowFatalError(FL("无法获取本地服务器的实际绑定地址。", "Failed to obtain the local server's bound address."));
                return;
            }
            Log.Info($"[Server] 静态资源服务器已绑定 {boundUrl}");
            appUrl = $"{boundUrl}/index.html?token={accessToken}";
        }

        // 获取系统 DPI 缩放比例
        float scale = GetSystemScale();

        // 不透明回退模式（--opaque / DECILAUNCHER_OPAQUE=1）：WebView2 透明合成异常时
        // 仍能拿到可见窗口；该模式同时启用 --disable-gpu，保留原本的省内存收益
        var opaque = IsOpaqueModeRequested(args, out var opaqueSource);

        // WebView2 用户数据目录必须在建窗之前指定（仅 Windows/WebView2 适用）
        if (OperatingSystem.IsWindows())
            EnsureWebView2UserDataFolder();

        // 构建并配置 Photino 窗口
        var window = BuildWindow(AppendOpaqueFlag(appUrl, opaque), scale, opaque);
        Log.Info($"[Window] 窗口已创建（{(opaque ? "不透明回退" : "透明")}模式{opaqueSource}）");

        // 启动日志（文件 + DEBUG 控制台）
        Log.Info($"Deci Launcher v{AppVersion} started");

        // 前端就绪看门狗：页面 JS 挂载后必然主动发消息（App.vue onMounted 共 5 条），
        // 若 15 秒内一条都没有，说明前端没渲染出来（dev server 异常 / 资源缺失 / WebView2 合成失败）——
        // 此时窗口是全透明且无法操作的，必须明确提示并关闭，而不是静默留下不可见窗口
        _ = Task.Run(async () =>
        {
            await Task.Delay(15_000);
            if (FrontendReady) return;
            Log.Warn("[Window] 15 秒内未收到前端任何消息：前端可能未渲染（检查 pnpm dev / 5173 占用 / WebView2 透明合成）");
            // 窗口仍停在屏幕外：先移回屏幕内，让用户看到"窗口 + 明确原因"，而不是什么都没有
            try { window.Invoke(() => window.Center()); }
            catch (Exception ex) { Log.Warn($"[Window] 移回窗口失败: {ex.Message}"); }
            ShowFatalError(FL(
                "前端界面在 15 秒内没有响应，窗口无法显示内容。\n\n" +
                "请检查：\n" +
                "  1) Debug 模式下 UserInterface 是否正在运行 pnpm dev；\n" +
                "  2) 5173 端口是否被其它程序占用；\n" +
                "  3) 若怀疑 WebView2 透明合成异常，请用 --opaque 参数启动（不透明窗口）。\n\n" +
                "点击确定后将关闭启动器。",
                "The frontend did not respond within 15 seconds.\n\n" +
                "Please check:\n" +
                "  1) In Debug builds, is pnpm dev running in UserInterface?\n" +
                "  2) Is port 5173 occupied by another program?\n" +
                "  3) If WebView2 transparency is suspected, start with --opaque.\n\n" +
                "The launcher will close when you click OK."));
            try { window.Invoke(() => window.Close()); }
            catch (Exception ex) { Log.Warn($"[Window] 关闭未渲染窗口失败: {ex.Message}"); }
        });

        // 阻塞主线程，等待窗口关闭（进入消息循环）
        window.WaitForClose();

        // 兜底清理：OS 级关闭（Alt+F4 / 任务栏关闭）不经过前端 close 消息，
        // 在此执行与 close 消息一致的清理——仅取消尚未创建进程的启动流程。
        // 已启动的游戏进程有意保留：关闭启动器不应中断玩家正在进行的游戏
        // （HMCL/PCL/官方启动器同此约定；进程由 OS 接管，输出管道断开不影响游戏运行）
        if (RunningProcess == null)
        {
            lock (CtsLock)
                LaunchCts.Cancel();
        }
    }

    // 访问控制会话 cookie 名称（token 校验通过后下发，静态资源请求凭此通过）
    private const string AccessCookieName = "DeciLauncherAccess";

    /// <summary>
    /// 从环境变量读取端口：默认 0（随机端口）。
    /// DECILAUNCHER_PORT 仅在 CI 构建（-p:CI_BUILD=true 注入编译常量）或 DEBUG 构建下生效，
    /// 供测试与诊断固定端口；普通用户运行时始终随机，避免恶意进程通过用户环境变量
    /// （如伪造 GITHUB_ACTIONS）预设已知端口
    /// </summary>
    private static int ReadConfiguredPort()
    {
        if (!AllowEnvironmentOverride()) return 0;
        var raw = Environment.GetEnvironmentVariable("DECILAUNCHER_PORT");
        return int.TryParse(raw, out var port) && port is >= 0 and <= 65535 ? port : 0;
    }

    /// <summary>
    /// 从环境变量读取访问 token：默认每次启动随机生成。
    /// DECILAUNCHER_TOKEN 仅在 CI 构建/DEBUG 下生效，且必须匹配 [A-Za-z0-9_-]{8,64}，
    /// 防止特殊字符破坏 URL 拼接或弱 token 被预置
    /// </summary>
    private static string ReadConfiguredToken()
    {
        if (AllowEnvironmentOverride())
        {
            var token = Environment.GetEnvironmentVariable("DECILAUNCHER_TOKEN");
            if (!string.IsNullOrEmpty(token) && token.Length is >= 8 and <= 64 &&
                token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                return token;
        }
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 环境变量覆盖仅在 CI 构建（编译期常量 CI_BUILD，不可被用户环境变量伪造）或 DEBUG 构建下允许。
    /// 此前依赖 GITHUB_ACTIONS 环境变量，任意同机进程可预置该变量伪造成 CI 环境
    /// </summary>
    private static bool AllowEnvironmentOverride() =>
#if CI_BUILD
        true;
#else
        IsDebugMode;
#endif

    /// <summary>
    /// 恒定时间字符串比较，避免 token/session 比较的时序侧信道
    /// </summary>
    private static bool TokensEqual(string a, string b) =>
        a.Length == b.Length &&
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a),
            System.Text.Encoding.UTF8.GetBytes(b));

    /// <summary>
    /// 解析 Debug 模式下的 Vite 开发服务器地址。
    /// 依次探测 127.0.0.1 与 localhost（避开 IPv6 解析差异），且要求响应内容含本项目标记——
    /// 仅判断"可达"会让端口被其它程序占用时把别人的页面装进我们的窗口
    /// </summary>
    private static bool TryResolveDevServerUrl(out string url)
    {
        foreach (var candidate in new[] { "http://127.0.0.1:5173", "http://localhost:5173" })
        {
            if (IsOurDevServer(candidate))
            {
                url = candidate;
                return true;
            }
        }
        url = "";
        return false;
    }

    /// <summary>
    /// 候选地址是否为「本项目」的 Vite 开发服务器：要求 HTTP 200 且 HTML 含
    /// index.html 的项目标记（页面标题或入口脚本路径）
    /// </summary>
    private static bool IsOurDevServer(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = client.GetAsync(url).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return false;
            var html = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return html.Contains("Deci Launcher", StringComparison.Ordinal)
                || html.Contains("/src/main.ts", StringComparison.Ordinal);
        }
        catch
        {
            // 连接失败/超时/证书错误等一律视为「不是本项目 dev server」
            return false;
        }
    }

    /// <summary>
    /// 是否请求不透明回退模式：命令行 --opaque 或环境变量 DECILAUNCHER_OPAQUE=1。
    /// 用途：WebView2 透明合成异常时仍能拿到可见窗口（该模式同时启用 --disable-gpu）
    /// </summary>
    private static bool IsOpaqueModeRequested(string[] args, out string source)
    {
        if (args.Any(a => string.Equals(a, "--opaque", StringComparison.OrdinalIgnoreCase)))
        {
            source = "，命令行 --opaque";
            return true;
        }
        if (Environment.GetEnvironmentVariable("DECILAUNCHER_OPAQUE") == "1")
        {
            source = "，环境变量 DECILAUNCHER_OPAQUE";
            return true;
        }
        source = "";
        return false;
    }

    /// <summary>在应用 URL 上追加 opaque=1 查询参数（前端据此给页面外层铺不透明底色）</summary>
    private static string AppendOpaqueFlag(string appUrl, bool opaque) =>
        opaque ? appUrl + (appUrl.Contains('?') ? "&" : "?") + "opaque=1" : appUrl;

    /// <summary>
    /// 为 WebView2 指定**实测可写**的用户数据目录，必须在创建窗口（即创建 WebView2 环境）之前调用。
    /// 背景（本机实测根因）：WebView2 默认使用与其他应用共享的用户数据目录；当该位置不存在且
    /// 无法创建、或不可写时，WebView2 **不会启动也不报错**——窗口创建成功但内容永不渲染，
    /// 表现为"窗口完全透明"。故这里按候选链挑第一个真正可写的目录：
    /// 1) %LOCALAPPDATA%\DeciLauncher（符合 Windows 约定的首选）；
    /// 2) exe 同级 DeciLauncher-data（便携运行，或受限环境只允许写程序目录时）；
    /// 3) %TEMP%\DeciLauncher（最后兜底）。
    /// WebView2 把该变量的值当作「父目录」，并在其中创建 EBWebView 子目录；
    /// Photino.NET 4.0.16 未暴露 UDF API，故通过 WEBVIEW2_USER_DATA_FOLDER 环境变量指定。
    /// </summary>
    private static void EnsureWebView2UserDataFolder()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeciLauncher"),
            Path.Combine(AppContext.BaseDirectory, "DeciLauncher-data"),
            Path.Combine(Path.GetTempPath(), "DeciLauncher")
        };

        foreach (var candidate in candidates)
        {
            if (!TryPrepareWritableDirectory(candidate, out var error))
            {
                Log.Warn($"[Window] WebView2 数据目录不可写，尝试下一个: {candidate}（{error}）");
                continue;
            }
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", candidate);
            Log.Info($"[Window] WebView2 用户数据目录: {Path.Combine(candidate, "EBWebView")}");
            return;
        }

        // 所有候选都不可写：不设置变量，让 WebView2 使用自身默认位置（很可能同样失败）
        Log.Warn("[Window] 未找到可写的 WebView2 数据目录，回退 WebView2 默认位置（窗口可能无法渲染内容）");
    }

    /// <summary>
    /// 创建目录并用探针文件验证**确实可写**——CreateDirectory 成功不代表可写：
    /// 受限环境里目录可能已存在但拒绝写入，而 WebView2 对此只会静默失败
    /// </summary>
    private static bool TryPrepareWritableDirectory(string dir, out string error)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, $".write-probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    // ===== 单实例保护 =====
    // Windows：命名 Mutex（内核对象，进程退出/崩溃自动释放，无残留锁问题，默认 Local 会话作用域）；
    // Linux：锁文件「FileShare.None 独占打开 + FileStream.Lock 字节区间锁」双重保障——
    // FileShare 的跨进程强制在不同平台/文件系统上并不保证一致，故在 Linux 上叠加 flock 排他锁；
    // macOS 不支持 FileStream.Lock，仅依赖 FileShare.None。
    // 两者均由 OS 在进程退出/崩溃时释放，不留陈旧锁。
    // 防止两个启动器实例同时管理同一 .minecraft 或并发启动游戏实例
    internal static Mutex? SingleInstanceMutex;
    internal static FileStream? SingleInstanceLockFile;

    /// <summary>
    /// 尝试获取单实例锁。返回 false 表示已有实例在运行。
    /// 锁机制自身异常时不阻断启动（与无锁时的既有行为一致，仅记录警告）
    /// </summary>
    private static bool AcquireSingleInstanceLock()
    {
        if (OperatingSystem.IsWindows())
        {
            var mutex = new Mutex(initiallyOwned: true, "DeciLauncher-SingleInstance", out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                return false;
            }
            SingleInstanceMutex = mutex; // 静态引用保活至进程退出
            return true;
        }

        try
        {
            Directory.CreateDirectory(AccountsDir);
            var stream = new FileStream(
                Path.Combine(AccountsDir, "launcher.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            // 显式字节区间锁兜底：Linux 取 flock 排他锁、Windows 取 LockFileEx，
            // 不依赖 FileShare 是否被平台强制；已被其他实例持有时抛 IOException。
            // macOS 上 FileStream.Lock 不受支持（CA1416，运行时抛 PlatformNotSupportedException），
            // 该平台仅依赖上面的 FileShare.None——不得省略守卫，否则异常会被外层 catch 吞掉并绕过检查
            if (!OperatingSystem.IsMacOS())
            {
                try
                {
                    stream.Lock(0, 1);
                }
                catch (IOException)
                {
                    stream.Dispose();
                    return false;
                }
            }

            SingleInstanceLockFile = stream; // 静态引用保活至进程退出
            return true;
        }
        catch (IOException)
        {
            // 打开即失败：FileShare.None 已被其他实例持有
            return false;
        }
        catch (Exception ex)
        {
            Log.Warn($"[SingleInstance] 锁文件创建失败（忽略并继续启动）: {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// 致命错误提示：Windows 弹原生消息框，其他平台输出到 stderr；同时落盘文件日志
    /// </summary>
    private static void ShowFatalError(string message)
    {
        Log.Warn($"[FATAL] {message}");
        if (OperatingSystem.IsWindows())
        {
            MessageBoxW(IntPtr.Zero, message, "Deci Launcher", 0x10 /* MB_ICONERROR */);
        }
        else
        {
            Console.Error.WriteLine($"[FATAL] {message}");
        }
    }
}
