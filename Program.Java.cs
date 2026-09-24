// Java 运行时自动检测
using MinecraftLaunch.Utilities;
// JSON 序列化（替换手拼 JSON）
using System.Text.Json;
// Photino 窗口（前端消息回传）
using Photino.NET;

namespace DeciLauncher;

partial class Program
{
    // Java 扫描结果缓存：启动时自动扫描一次后复用，仅用户手动刷新（force）时重新全盘枚举，
    // 减少重复扫描的临时对象与 GC 压力
    private static List<object>? CachedJavaItems;

    // Java 扫描代次：与 ScanGamesVersion 同一模式——用户连点两次「刷新」时，
    // 先完成的旧扫描不得覆盖新缓存或乱序回发过期结果
    private static int ScanJavaVersion;

    /// <summary>
    /// 扫描系统中已安装的 Java 运行时，通过 WebView 回传给前端。
    /// force=false 且已有缓存时直接复用缓存，避免重复全盘枚举
    /// </summary>
    private static async Task ScanJavaAsync(PhotinoWindow window, bool force)
    {
        // 捕获本次扫描的代次，缓存写入与结果回发前校验是否仍为最新请求
        var generation = Interlocked.Increment(ref ScanJavaVersion);
        bool IsStale() => generation != Volatile.Read(ref ScanJavaVersion);

        try
        {
            var cached = Volatile.Read(ref CachedJavaItems);
            if (!force && cached != null)
            {
                if (!IsStale())
                    TryNotifyWindow(window, JsonSerializer.Serialize(new
                    {
                        type = "java-list",
                        javas = cached
                    }));
                return;
            }

            var items = new List<object>();

            await foreach (var java in JavaUtil.EnumerableJavaAsync())
            {
                items.Add(new
                {
                    path = java.JavaPath,
                    version = java.JavaVersion ?? ""
                });
            }

            // 已被更新请求取代：丢弃结果，避免旧扫描覆盖新缓存或乱序回发
            if (IsStale()) return;

            Volatile.Write(ref CachedJavaItems, items);
            TryNotifyWindow(window, JsonSerializer.Serialize(new
            {
                type = "java-list",
                javas = items
            }));
        }
        catch (Exception ex)
        {
            if (IsStale()) return;
            Log.Warn($"[Java] 扫描失败: {ex.Message}");
            TryNotifyWindow(window, JsonSerializer.Serialize(new
            {
                type = "java-error",
                message = ex.Message
            }));
        }
    }
}
