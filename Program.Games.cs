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
    /// 读取指定 .minecraft 目录下已安装的游戏版本，通过 WebView 回传给前端。
    /// 解析在后台线程执行，避免在 Photino 消息线程上同步阻塞 UI；
    /// 通过递增版本号丢弃过期的扫描结果，避免并发扫描乱序覆盖
    /// </summary>
    private static int ScanGamesVersion;

    private static void ScanGames(PhotinoWindow window, string minecraftPath)
    {
        // 捕获本次扫描的版本号，结果发送前校验是否仍为最新请求
        var version = Interlocked.Increment(ref ScanGamesVersion);
        bool IsStale() => version != Volatile.Read(ref ScanGamesVersion);

        _ = Task.Run(() =>
        {
            var games = new List<object>();

            try
            {
                var versionsDir = Path.Combine(minecraftPath, "versions");
                if (!Directory.Exists(versionsDir))
                {
                    // 扫描是只读操作：目录不存在时直接返回空列表，不创建任何目录
                    if (!IsStale())
                        TryNotifyWindow(window, JsonSerializer.Serialize(new
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
                        Log.Debug($"[WARN] 跳过游戏 {versionId}: {ex.Message}");
                    }
                }

                if (!IsStale())
                    TryNotifyWindow(window, JsonSerializer.Serialize(new
                    {
                        type = "game-list",
                        path = minecraftPath,
                        games
                    }));
            }
            catch (Exception ex)
            {
                Log.Debug($"[WARN] 扫描游戏失败: {ex.Message}");
                if (!IsStale())
                    TryNotifyWindow(window, JsonSerializer.Serialize(new
                    {
                        type = "game-list",
                        path = minecraftPath,
                        games = Array.Empty<object>()
                    }));
            }
        });
    }

    /// <summary>
    /// 打开系统文件夹选择器，让用户选择 .minecraft 目录
    /// 返回 Task 而非 async void：异常由内部 try/catch 全量捕获并转 game-error 消息，
    /// 调用方以 fire-and-forget（_ = ...）方式投递
    /// </summary>
    private static async Task PickGamePathAsync(PhotinoWindow window)
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
                TryNotifyWindow(window, message);
            }
        }
        catch (Exception ex)
        {
            var message = JsonSerializer.Serialize(new
            {
                type = "game-error",
                message = $"{L("选择目录失败", "Failed to pick directory")}: {ex.Message}"
            });
            TryNotifyWindow(window, message);
        }
    }
}
