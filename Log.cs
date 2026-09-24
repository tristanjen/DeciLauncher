// 集中日志入口：全部日志输出收敛于此（此前散落的 System.Diagnostics.Debug.WriteLine）。
// - Debug：[Conditional("DEBUG")]，仅 DEBUG 构建输出到调试器/控制台，Release 零开销（调用被完全剥除）；
// - Info/Warn：始终写入文件日志（InitializeFile），DEBUG 构建附加控制台输出——
//   Release 构建下用户侧问题（启动失败、崩溃分析异常等）可凭 latest.log 追溯。

using System.Diagnostics;

namespace DeciLauncher;

internal static class Log
{
    private static readonly object FileLock = new();
    private static StreamWriter? FileLogger;

    /// <summary>
    /// 记录诊断日志（仅 DEBUG 构建生效；message 保留调用方既有前缀如 [Launch]/[MC]）
    /// </summary>
    [Conditional("DEBUG")]
    internal static void Debug(string message) => System.Diagnostics.Debug.WriteLine(message);

    /// <summary>信息级日志：写入文件；DEBUG 构建附加控制台输出</summary>
    internal static void Info(string message) => Write("INFO", message);

    /// <summary>警告级日志：写入文件（Release 保留，供用户侧诊断）；DEBUG 构建附加控制台输出</summary>
    internal static void Warn(string message) => Write("WARN", message);

    private static void Write(string level, string message)
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine(message);
#endif
        // 镜像到 stderr：dotnet run 在终端即可看到 INFO/WARN（DEBUG 的 Debug.WriteLine 只进调试器）。
        // GUI 进程无控制台时 .NET 会丢弃输出，异常也被吞掉，不影响正常运行
        try { Console.Error.WriteLine($"[{level}] {message}"); } catch { /* 无控制台时忽略 */ }

        var logger = Volatile.Read(ref FileLogger);
        if (logger == null) return;
        try
        {
            lock (FileLock)
                logger.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}");
        }
        catch { /* 文件写入失败不致命 */ }
    }

    /// <summary>
    /// 初始化文件日志（Debug/Release 均启用），按候选链挑第一个可写位置：
    /// 1) %AppData%\.decilc\logs\latest.log（首选）；
    /// 2) exe 同级 logs\latest.log（便携运行或受限环境禁止写用户配置目录时）。
    /// 受限环境下首选位置会直接抛 UnauthorizedAccessException，此前只能放弃文件日志，
    /// 导致"窗口空白但没有任何日志"无法诊断——回退后仍能留下痕迹。
    /// 启动时滚动：latest.log → latest.1.log → latest.2.log（保留 3 份历史）。
    /// 初始化失败不致命：仅失去文件诊断能力，控制台输出不受影响
    /// </summary>
    internal static void InitializeFile()
    {
        Exception? lastError = null;
        foreach (var dir in new[]
        {
            Path.Combine(Program.AccountsDir, "logs"),
            Path.Combine(AppContext.BaseDirectory, "logs")
        })
        {
            try
            {
                Directory.CreateDirectory(dir);
                var latest = Path.Combine(dir, "latest.log");
                var older1 = Path.Combine(dir, "latest.1.log");
                var older2 = Path.Combine(dir, "latest.2.log");
                try
                {
                    if (File.Exists(older1)) File.Move(older1, older2, true);
                    if (File.Exists(latest)) File.Move(latest, older1, true);
                }
                catch { /* 滚动失败不阻塞初始化 */ }

                FileLogger = new StreamWriter(latest, append: true, System.Text.Encoding.UTF8) { AutoFlush = true };
                FileLogger.WriteLine($"===== Deci Launcher v{Program.AppVersion} | {DateTime.Now:yyyy-MM-dd HH:mm:ss} | {latest} =====");
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        FileLogger = null;
        // 日志系统自身的失败必须可见（此前只写 Debug.WriteLine，终端/用户侧完全看不到）
        System.Diagnostics.Debug.WriteLine($"[WARN] 文件日志初始化失败: {lastError?.Message}");
        try { Console.Error.WriteLine($"[WARN] 文件日志初始化失败（所有候选目录均不可写）: {lastError}"); } catch { /* 无控制台时忽略 */ }
    }
}
