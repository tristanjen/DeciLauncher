// MinecraftLaunch 库的 init-only 属性反射赋值收敛点
// 背景:库的 JavaEntry.JavaPath / MinecraftProcess.Process 均为 init-only 属性,
// 构造后无公开途径修改,启动器不得已用反射覆盖。全部反射 hack 收敛于此,便于
// 后续库版本升级时统一替换为公开 API。

using System.Reflection;
using MinecraftLaunch.Base.Models.Game;
using MinecraftLaunch.Launch;

namespace DeciLauncher;

internal static class MinecraftLaunchFallbacks
{
    /// <summary>
    /// 覆盖 JavaEntry.JavaPath(init-only,无公开 setter)。
    /// Windows 下将 java.exe 替换为 javaw.exe,避免启动游戏时弹出控制台黑框。
    /// 属性不存在时静默跳过(与原反射代码的 null 检查行为一致)。
    /// </summary>
    internal static void OverrideJavaPath(JavaEntry java, string javaPath)
    {
        typeof(JavaEntry).GetProperty("JavaPath")?.SetValue(java, javaPath);
    }

    /// <summary>
    /// 注入 MinecraftProcess.Process(init-only,无公开 setter)。
    /// MinecraftRunner.RunAsync 因库内部 ParseJsonNode bug 提前返回时未创建进程,
    /// fallback 路径独立构造 System.Diagnostics.Process 后只能反射注入。
    /// 属性不存在时静默跳过(与原反射代码的 null 检查行为一致)。
    /// </summary>
    internal static void AttachProcess(MinecraftProcess minecraftProcess, System.Diagnostics.Process process)
    {
        typeof(MinecraftProcess).GetProperty("Process")?.SetValue(minecraftProcess, process);
    }
}
