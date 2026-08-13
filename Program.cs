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
    public static bool IsDebugMode = true;
#else
    public static bool IsDebugMode = false;
#endif

    // ===== 日志系统（仅 DEBUG 模式） =====

#if DEBUG
    // 日志工厂：创建控制台日志提供程序
    private static readonly ILoggerFactory LoggerFactory =
        Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddConsole());

    // 日志记录器实例
    private static readonly ILogger Logger =
        LoggerFactory.CreateLogger(nameof(DeciLauncher));
#endif

    // ===== 应用入口点 =====

    // STAThread：Windows COM 互操作要求（WebView2 底层依赖）
    [STAThread]
    static void Main(string[] args)
    {
        // Windows 下解除 DPI 虚拟化以获取真实系统缩放值
        if (OperatingSystem.IsWindows())
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_SYSTEM_AWARE);

        // MinecraftLaunch 全局初始化（下载线程、重试、UserAgent 等）
        InitializeHelper.Initialize(settings =>
        {
            settings.MaxThread = 256;
            settings.MaxFragment = 128;
            settings.MaxRetryCount = 4;
            settings.IsEnableMirror = false;
            settings.IsEnableFragment = false;
            settings.UserAgent = "DeciLauncher/1.0";
        });

        // 从 Config/accounts.json 加载已保存的账户
        InitializeAccounts();

        // Release 模式：用 ManifestEmbeddedFileProvider 从 DLL 内嵌资源提供前端文件
        // 不创建物理 wwwroot 目录
        string appUrl;
        if (IsDebugMode)
        {
            // 先探测 Vite 开发服务器是否可达，避免窗口白屏且无任何提示
            if (!IsLocalServerReachable("http://localhost:5173"))
            {
                ShowFatalError(
                    "无法连接前端开发服务器 (http://localhost:5173)。\n\n" +
                    "请先在 UserInterface 目录运行 pnpm dev 后重新启动。");
                return;
            }
            appUrl = "http://localhost:5173";
        }
        else
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                WebRootPath = AppContext.BaseDirectory
            });
            var assembly = Assembly.GetEntryAssembly();
            if (assembly != null)
            {
                var embeddedProvider = new ManifestEmbeddedFileProvider(
                    assembly, "Resources/wwwroot");
                builder.Environment.WebRootFileProvider = embeddedProvider;
            }

            // 安全：仅绑定 IPv4 回环地址，默认随机端口（0 = 由 Kestrel 自动分配），
            // 防止此前固定 8000 端口被同机其他程序/浏览器直接访问。
            // DECILAUNCHER_PORT / DECILAUNCHER_TOKEN 环境变量仅供 CI 与诊断固定取值
            var port = ReadConfiguredPort();
            var accessToken = ReadConfiguredToken();

            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
            var app = builder.Build();

            // 访问控制中间件：首次导航必须携带 token（query 参数），校验通过后
            // 下发 HttpOnly 会话 cookie，后续静态资源请求凭 cookie 通过；
            // 无凭据请求一律 404（不泄露服务存在性），阻止本机任意程序直接访问前端。
            // Referrer-Policy: no-referrer 防止 token 随 Referer 头泄露到任何外部站点
            app.Use(async (context, next) =>
            {
                var token = context.Request.Query["token"].ToString();
                if (token == accessToken)
                {
                    context.Response.Cookies.Append(AccessCookieName, accessToken, new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict
                    });
                    context.Response.Headers["Referrer-Policy"] = "no-referrer";
                    await next();
                    return;
                }
                if (context.Request.Cookies[AccessCookieName] == accessToken)
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
                System.Diagnostics.Debug.WriteLine($"[FATAL] Web 服务器启动失败: {ex}");
                ShowFatalError($"无法在 127.0.0.1:{port} 启动本地服务器，请确认端口未被占用。\n\n{ex.Message}");
                return;
            }

            // 随机端口模式下从 Kestrel 读取实际绑定地址（port=0 时此处为已分配的真实端口）
            var boundUrl = app.Urls.FirstOrDefault(u =>
                u.StartsWith("http://127.0.0.1", StringComparison.Ordinal));
            if (boundUrl == null)
            {
                ShowFatalError("无法获取本地服务器的实际绑定地址。");
                return;
            }
            appUrl = $"{boundUrl}/index.html?token={accessToken}";
        }

        // 获取系统 DPI 缩放比例
        float scale = GetSystemScale();

        // 构建并配置 Photino 窗口
        var window = BuildWindow(appUrl, scale);

        // DEBUG 模式下输出启动日志
#if DEBUG
        Logger.LogInformation("Deci Launcher v0.1.0-beta.1 started");
#endif

        // 阻塞主线程，等待窗口关闭（进入消息循环）
        window.WaitForClose();
    }

    // 访问控制会话 cookie 名称（token 校验通过后下发，静态资源请求凭此通过）
    private const string AccessCookieName = "DeciLauncherAccess";

    /// <summary>
    /// 从环境变量读取端口：默认 0（随机端口）；DECILAUNCHER_PORT 供 CI/诊断固定端口
    /// </summary>
    private static int ReadConfiguredPort()
    {
        var raw = Environment.GetEnvironmentVariable("DECILAUNCHER_PORT");
        return int.TryParse(raw, out var port) && port is >= 0 and <= 65535 ? port : 0;
    }

    /// <summary>
    /// 从环境变量读取访问 token：默认每次启动随机生成；DECILAUNCHER_TOKEN 供 CI/诊断固定 token
    /// </summary>
    private static string ReadConfiguredToken()
    {
        var token = Environment.GetEnvironmentVariable("DECILAUNCHER_TOKEN");
        return string.IsNullOrEmpty(token) ? Guid.NewGuid().ToString("N") : token;
    }

    /// <summary>
    /// 探测本地 HTTP 服务器是否可达（DEBUG 模式下检查 Vite 开发服务器）
    /// </summary>
    private static bool IsLocalServerReachable(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = client.GetAsync(url).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 致命错误提示：Windows 弹原生消息框，其他平台输出到 stderr
    /// </summary>
    private static void ShowFatalError(string message)
    {
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
