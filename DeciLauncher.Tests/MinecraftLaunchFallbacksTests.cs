// MinecraftLaunchFallbacks 单元测试：锁定反射赋值收敛点的行为。
// OverrideJavaPath: JavaEntry.JavaPath(init-only)→ 反射覆盖后 getter 返回新值
// AttachProcess: MinecraftProcess.Process(init-only)→ 反射注入后 getter 返回同一实例
//
// MinecraftProcess 构造函数需要完整 LaunchConfig/MinecraftEntry，测试用
// RuntimeHelpers.GetUninitializedObject 跳过构造函数只验证属性注入行为。

using System.Runtime.CompilerServices;
using DeciLauncher;
using MinecraftLaunch.Base.Models.Game;
using MinecraftLaunch.Launch;

namespace DeciLauncher.Tests;

public class MinecraftLaunchFallbacksTests
{
    [Fact]
    public void OverrideJavaPath_ReplacesValue()
    {
        var java = new JavaEntry { JavaPath = @"C:\jdk\bin\java.exe" };
        MinecraftLaunchFallbacks.OverrideJavaPath(java, @"C:\jdk\bin\javaw.exe");
        Assert.Equal(@"C:\jdk\bin\javaw.exe", java.JavaPath);
    }

    [Fact]
    public void AttachProcess_InjectsSameProcessInstance()
    {
        var mcProcess = (MinecraftProcess)RuntimeHelpers.GetUninitializedObject(typeof(MinecraftProcess));
        // 未启动的 System.Diagnostics.Process 可安全构造（仅属性注入验证）
        using var proc = new System.Diagnostics.Process();
        proc.StartInfo.FileName = "dummy.exe";

        MinecraftLaunchFallbacks.AttachProcess(mcProcess, proc);
        Assert.Same(proc, mcProcess.Process);
    }
}
