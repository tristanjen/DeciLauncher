// 集中日志入口：全部日志输出收敛于此（此前散落的 System.Diagnostics.Debug.WriteLine）。
// [Conditional("DEBUG")] 保证 DEBUG 构建输出到调试器/控制台、RELEASE 构建零开销（调用被完全剥除），
// 与原先直接调用 System.Diagnostics.Debug.WriteLine 的行为完全一致。

using System.Diagnostics;

namespace DeciLauncher;

internal static class Log
{
    /// <summary>
    /// 记录诊断日志（仅 DEBUG 构建生效；message 保留调用方既有前缀如 [Launch]/[WARN]）
    /// </summary>
    [Conditional("DEBUG")]
    internal static void Debug(string message) => System.Diagnostics.Debug.WriteLine(message);
}
