// 反射（获取入口程序集以读取内嵌资源）
using System.Reflection;
// ASP.NET Core 最小化 API（Release 模式提供内嵌静态文件）
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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

            builder.WebHost.UseUrls("http://localhost:8000");
            var app = builder.Build();
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
                ShowFatalError($"无法在端口 8000 启动本地服务器，请确认端口未被占用。\n\n{ex.Message}");
                return;
            }
            appUrl = "http://localhost:8000/index.html";
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
