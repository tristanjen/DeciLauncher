// .minecraft 目录解析（匹配版本对应的 Java）
using MinecraftLaunch.Components.Parser;
// GetAppropriateJava 扩展方法
using MinecraftLaunch.Extensions;
// Java 运行时自动检测
using MinecraftLaunch.Utilities;
// JSON 序列化（替换手拼 JSON）
using System.Text.Json;
// Photino 窗口（前端消息回传）
using Photino.NET;

namespace DeciLauncher;

partial class Program
{
    /// <summary>
    /// 扫描系统中已安装的 Java 运行时，通过 WebView 回传给前端
    /// </summary>
    private static async Task ScanJavaAsync(PhotinoWindow window)
    {
        try
        {
            var items = new List<object>();

            await foreach (var java in JavaUtil.EnumerableJavaAsync())
            {
                items.Add(new
                {
                    path = java.JavaPath,
                    version = java.JavaVersion ?? ""
                });
            }

            var message = JsonSerializer.Serialize(new
            {
                type = "java-list",
                javas = items
            });
            window.Invoke(() => window.SendWebMessage(message));
        }
        catch (Exception ex)
        {
            var message = JsonSerializer.Serialize(new
            {
                type = "java-error",
                message = ex.Message
            });
            window.Invoke(() => window.SendWebMessage(message));
        }
    }

    /// <summary>
    /// 根据 Minecraft 版本自动匹配本机最合适的 Java 运行时
    /// </summary>
    private static string? FindAppropriateJava(string minecraftId, string minecraftPath)
    {
        try
        {
            var parser = new MinecraftParser(minecraftPath);
            var entry = parser.GetMinecraft(minecraftId);
            if (entry is null) return null;

            var javaList = JavaUtil.EnumerableJavaAsync().ToBlockingEnumerable().ToList();
            return entry.GetAppropriateJava(javaList)?.JavaPath;
        }
        catch
        {
            return null;
        }
    }
}
