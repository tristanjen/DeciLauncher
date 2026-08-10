// .minecraft 目录解析（读取已安装的游戏版本）
using MinecraftLaunch.Components.Parser;
// ModifiedMinecraftEntry / ModLoaderInfo 类型
using MinecraftLaunch.Base.Models.Game;
// ModLoaderType 枚举（用于 NeoForge 特殊处理）
using MinecraftLaunch.Base.Enums;
// JSON 序列化（替换手拼 JSON）
using System.Text.Json;
// Photino 窗口（前端消息回传 + 文件夹选择器）
using Photino.NET;

namespace DeciLauncher;

partial class Program
{
    /// <summary>
    /// 读取指定 .minecraft 目录下已安装的游戏版本，通过 WebView 回传给前端
    /// </summary>
    private static void ScanGames(PhotinoWindow window, string minecraftPath)
    {
        var games = new List<object>();

        var versionsDir = Path.Combine(minecraftPath, "versions");
        if (!Directory.Exists(versionsDir))
        {
            try { Directory.CreateDirectory(minecraftPath); } catch { /* 权限不足时静默跳过，不影响启动 */ }
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "game-list",
                path = minecraftPath,
                games = Array.Empty<object>()
            }));
            return;
        }

        var parser = new MinecraftParser(minecraftPath);

        foreach (var dir in Directory.GetDirectories(versionsDir))
        {
            var versionId = Path.GetFileName(dir);
            try
            {
                var game = parser.GetMinecraft(versionId);
                if (game is null) continue;

                var loader = "";

                if (game is ModifiedMinecraftEntry modded)
                {
                    var loaders = modded.ModLoaders.Select(ml =>
                    {
                        var version = ml.Version;
                        if (ml.Type == ModLoaderType.NeoForge)
                        {
                            var idx = game.Id.LastIndexOf("NeoForge_");
                            if (idx >= 0)
                                version = game.Id[(idx + "NeoForge_".Length)..];
                        }
                        return string.IsNullOrEmpty(version) ? $"{ml.Type}" : $"{ml.Type} {version}";
                    }).ToArray();
                    loader = string.Join(" + ", loaders);
                }

                games.Add(new
                {
                    id = game.Id,
                    isVanilla = game.IsVanilla,
                    mcVersion = game.Version.VersionId,
                    loader
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] 跳过游戏 {versionId}: {ex.Message}");
            }
        }

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "game-list",
            path = minecraftPath,
            games
        }));
    }

    /// <summary>
    /// 打开系统文件夹选择器，让用户选择 .minecraft 目录
    /// </summary>
    private static async void PickGamePathAsync(PhotinoWindow window)
    {
        try
        {
            var results = await window.ShowOpenFolderAsync("选择 .minecraft 目录", "", false);
            if (results != null && results.Length > 0)
            {
                var message = JsonSerializer.Serialize(new
                {
                    type = "game-path-selected",
                    path = results[0]
                });
                window.Invoke(() => window.SendWebMessage(message));
            }
        }
        catch (Exception ex)
        {
            var message = JsonSerializer.Serialize(new
            {
                type = "game-error",
                message = ex.Message
            });
            window.Invoke(() => window.SendWebMessage(message));
        }
    }
}
