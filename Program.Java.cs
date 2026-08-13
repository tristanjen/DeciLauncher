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

    /// <summary>
    /// 扫描系统中已安装的 Java 运行时，通过 WebView 回传给前端。
    /// force=false 且已有缓存时直接复用缓存，避免重复全盘枚举
    /// </summary>
    private static async Task ScanJavaAsync(PhotinoWindow window, bool force)
    {
        try
        {
            if (!force && CachedJavaItems != null)
            {
                TryNotifyWindow(window, JsonSerializer.Serialize(new
                {
                    type = "java-list",
                    javas = CachedJavaItems
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

            CachedJavaItems = items;
            var message = JsonSerializer.Serialize(new
            {
                type = "java-list",
                javas = items
            });
            TryNotifyWindow(window, message);
        }
        catch (Exception ex)
        {
            var message = JsonSerializer.Serialize(new
            {
                type = "java-error",
                message = ex.Message
            });
            TryNotifyWindow(window, message);
        }
    }
}
