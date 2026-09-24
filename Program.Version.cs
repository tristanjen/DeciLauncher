// 应用版本号（单一来源：csproj 的 <Version> → 程序集的 InformationalVersion）。
// UserAgent / 启动日志 / 关于页（get-app-info）/ 文件日志页眉共用，
// SourceLink 追加的 +commitHash 后缀在此截断。
// 独立成文件：Log 等基础组件依赖它，且让"版本号单一来源"的改动可以单独成一个提交。

using System.Reflection;

namespace DeciLauncher;

partial class Program
{
    /// <summary>应用版本号（形如 1.0.0-beta.2，已截断 SourceLink 的 +commitHash 后缀）</summary>
    internal static string AppVersion { get; } =
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion?.Split('+')[0] ?? "1.0.0";
}
