// 离线账户认证
using MinecraftLaunch.Components.Authenticator;
// .minecraft 解析 + 启动
using MinecraftLaunch.Components.Parser;
using MinecraftLaunch.Launch;
// 游戏/Java 数据模型
using MinecraftLaunch.Base.Models.Game;
// Java 自动检测 + 版本匹配
using MinecraftLaunch.Extensions;
using MinecraftLaunch.Utilities;
// JSON 解析（AOT 安全的 JsonDocument）
using System.Text.Json;
// 反射（设置 MinecraftProcess.Process）
using System.Reflection;
// Photino 窗口（前端消息回传）
using Photino.NET;

namespace DeciLauncher;

partial class Program
{
    /// <summary>
    /// 当前正在运行的 Minecraft 进程（用于关闭游戏）
    /// </summary>
    private static MinecraftProcess? RunningProcess;
    /// <summary>
    /// 当前启动任务的取消令牌（支持在 RunAsync 期间取消）
    /// </summary>
    private static CancellationTokenSource LaunchCts = new();

    /// <summary>
    /// 关闭正在运行的游戏（同时供 CancelLaunch 复用）
    /// </summary>
    private static void CloseGame(PhotinoWindow window)
    {
        if (RunningProcess == null) return;
        try
        {
            RunningProcess.Close();
            RunningProcess.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Launch] 关闭游戏失败: {ex.Message}");
        }
        RunningProcess = null;
        window.Invoke(() =>
            window.SendWebMessage("{\"type\":\"game-exited\"}"));
    }

    /// <summary>
    /// 取消正在进行的游戏启动（取消 RunAsync + 关闭已启动的进程）
    /// </summary>
    private static void CancelLaunch(PhotinoWindow window)
    {
        LaunchCts.Cancel();
        CloseGame(window);
        // 启动尚未完成（RunningProcess 为空）时 CloseGame 直接返回，仍需通知前端复位状态
        if (RunningProcess == null)
            window.Invoke(() =>
                window.SendWebMessage("{\"type\":\"game-exited\"}"));
    }

    /// <summary>
    /// 执行游戏启动流程：查找账户 → 解析游戏 → 匹配 Java → 启动
    /// </summary>
    private static async void LaunchGame(
        PhotinoWindow window,
        string gameId, string accountUuid,
        string javaPath, int maxMemory,
        string minecraftPath)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[Launch] 游戏: {gameId}");
            System.Diagnostics.Debug.WriteLine($"[Launch] 账户 UUID: {accountUuid}");
            System.Diagnostics.Debug.WriteLine($"[Launch] Java: {javaPath}");
            System.Diagnostics.Debug.WriteLine($"[Launch] 内存: {maxMemory} MB");
            System.Diagnostics.Debug.WriteLine($"[Launch] 路径: {minecraftPath}");

            // 重建取消令牌（上次启动可能已取消）
            LaunchCts = new CancellationTokenSource();
            var launchToken = LaunchCts.Token;

            if (maxMemory < 512) maxMemory = 512;
            if (maxMemory > 16384) maxMemory = 16384;

            // 1. 查找账户（优先使用缓存，避免每次重新 Authenticate 导致 UUID 不一致）
            var accountEntry = Accounts.FirstOrDefault(a => a.Uuid == accountUuid);
            if (accountEntry == null)
            {
                window.Invoke(() =>
                    window.SendWebMessage(JsonSerializer.Serialize(new { type = "game-error", message = "未找到选中的账户" })));
                return;
            }
            if (launchToken.IsCancellationRequested) { await CleanupCancelled(window); return; }
            if (!AuthenticatedAccounts.TryGetValue(accountEntry.Uuid, out var account))
            {
                account = new OfflineAuthenticator().Authenticate(accountEntry.Username);
                AuthenticatedAccounts[accountEntry.Uuid] = account;
            }

            // 2. 解析游戏版本
            var parser = new MinecraftParser(minecraftPath);
            var game = parser.GetMinecraft(gameId);
            if (game == null)
            {
                window.Invoke(() =>
                    window.SendWebMessage(JsonSerializer.Serialize(new { type = "game-error", message = "未找到选中的游戏版本" })));
                return;
            }
            if (launchToken.IsCancellationRequested) { await CleanupCancelled(window); return; }

            // 3. 查找 Java 运行时（后台线程执行，避免阻塞 UI）
            var javas = await Task.Run(() => JavaUtil.EnumerableJavaAsync().ToBlockingEnumerable().ToList(), launchToken);
            if (launchToken.IsCancellationRequested) { await CleanupCancelled(window); return; }
            MinecraftLaunch.Base.Models.Game.JavaEntry? java = javaPath switch
            {
                "__auto__" or "" => game.GetAppropriateJava(javas),
                _ => javas.FirstOrDefault(j => j.JavaPath == javaPath)
            };
            if (java == null)
            {
                window.Invoke(() =>
                    window.SendWebMessage(JsonSerializer.Serialize(new { type = "game-error", message = "未找到合适的 Java 运行时" })));
                return;
            }

            // Windows 下使用 javaw.exe 避免启动时弹出控制台黑框
            if (OperatingSystem.IsWindows())
            {
                var pathProp = typeof(MinecraftLaunch.Base.Models.Game.JavaEntry).GetProperty("JavaPath");
                if (pathProp != null)
                    pathProp.SetValue(java, java.JavaPath.Replace("java.exe", "javaw.exe"));
            }

            // 4. 构建启动配置
            var config = new LaunchConfig
            {
                Account = account,
                MaxMemorySize = maxMemory,
                MinMemorySize = 512,
                JavaPath = java,
                LauncherName = "DeciLauncher",
                NativesFolder = Path.Combine(Path.GetTempPath(), "DeciLauncher", "natives")
            };

            // 5. 启动游戏
            var runner = new MinecraftRunner(config, parser);
            System.Diagnostics.Debug.WriteLine("[Launch] 正在调用 RunAsync...");
            try
            {
                RunningProcess = await runner.RunAsync(gameId, launchToken);
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[Launch] 启动已取消");
                await CleanupCancelled(window);
                return;
            }
            catch (Exception rex)
            {
                System.Diagnostics.Debug.WriteLine($"[Launch] RunAsync 异常: {rex}");
                window.Invoke(() =>
                    window.SendWebMessage(JsonSerializer.Serialize(new { type = "game-error", message = $"启动异常: {rex.Message}" })));
                return;
            }
            if (launchToken.IsCancellationRequested)
            {
                await CleanupCancelled(window);
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[Launch] ArgumentList 数量: {RunningProcess.ArgumentList.Count()}");
            foreach (var a in RunningProcess.ArgumentList.Take(5))
                System.Diagnostics.Debug.WriteLine($"[Launch] Arg: {a}");

            // 空参数列表：Fabric/Quilt 等模组版本在 MinecraftRunner 内部 Parse 失败
            // 直接用 ArgumentsParser 独立生成参数，绕过内部可能的异常吞没
            if (RunningProcess.Process == null)
            {
                System.Diagnostics.Debug.WriteLine("[Launch] 参数列表为空，独立构造 ArgumentsParser");
                try
                {
                    var argParser = new ArgumentsParser(game, config);
                    var arguments = argParser.Parse().ToList();
                    System.Diagnostics.Debug.WriteLine($"[Launch] 独立 Parse 结果: {arguments.Count} 项");
                    foreach (var a in arguments.Take(3))
                        System.Diagnostics.Debug.WriteLine($"[Launch] Arg: {a}");

                    if (arguments.Count == 0)
                        throw new InvalidOperationException("ArgumentsParser 返回空参数列表");

                    var proc = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo(java.JavaPath)
                        {
                            Arguments = string.Join(' ', arguments),
                            WorkingDirectory = Path.Combine(minecraftPath, "versions", gameId),
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        },
                        EnableRaisingEvents = true
                    };
                    var procProp = typeof(MinecraftProcess).GetProperty("Process");
                    procProp?.SetValue(RunningProcess, proc);
                    if (launchToken.IsCancellationRequested) { await CleanupCancelled(window); return; }
                    RunningProcess.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Launch] ArgumentsParser 失败: {ex.Message}");
                    // Fallback：手动构造启动参数，绕过 MinecraftLaunch 的 ParseJsonNode bug
                    try
                    {
                        var fallbackArgs = BuildFallbackArgs(game, config, java, minecraftPath);
                        System.Diagnostics.Debug.WriteLine($"[Launch] Fallback 参数: {fallbackArgs.Count} 项");
                        var proc = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo(java.JavaPath)
                            {
                                Arguments = string.Join(' ', fallbackArgs),
                                WorkingDirectory = Path.Combine(minecraftPath, "versions", gameId),
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            },
                            EnableRaisingEvents = true
                        };
                        var procProp2 = typeof(MinecraftProcess).GetProperty("Process");
                        procProp2?.SetValue(RunningProcess, proc);

                        // 手动绑定控制台输出（MinecraftProcess 内部事件在构造函数提前 return 后未绑定）
                        proc.OutputDataReceived += (_, e) => {
                            if (!string.IsNullOrEmpty(e.Data))
                                System.Diagnostics.Debug.WriteLine($"[MC] {e.Data}");
                        };
                        proc.ErrorDataReceived += (_, e) => {
                            if (!string.IsNullOrEmpty(e.Data))
                                System.Diagnostics.Debug.WriteLine($"[MC] ERR: {e.Data}");
                        };
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();

                        // 手动绑定退出事件（MinecraftProcess 内置回调在构造器提前 return 后未绑定）
                        proc.Exited += (_, _) =>
                        {
                            System.Diagnostics.Debug.WriteLine("[MC] 游戏进程已退出（fallback 路径）");
                            proc.Dispose();
                            RunningProcess = null;
                            window.Invoke(() =>
                                window.SendWebMessage("{\"type\":\"game-exited\"}"));
                        };

                        if (launchToken.IsCancellationRequested) { await CleanupCancelled(window); return; }
                        RunningProcess.Start();
                    }
                    catch (Exception fallbackEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Launch] 手动启动失败: {fallbackEx}");
                        window.Invoke(() =>
                            window.SendWebMessage(JsonSerializer.Serialize(new { type = "game-error", message = $"启动失败: {fallbackEx.Message}" })));
                        return;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("[Launch] RunAsync 完成");

            // 注册日志和退出事件
            RunningProcess.OutputLogReceived += (_, arg) =>
                System.Diagnostics.Debug.WriteLine($"[MC] {arg.Data.Log}");

            var processRef = RunningProcess;
            RunningProcess.Exited += (_, _) =>
            {
                System.Diagnostics.Debug.WriteLine($"[MC] 游戏进程已退出，退出码: {processRef.Process?.ExitCode}");
                processRef?.Dispose();
                if (RunningProcess == processRef) RunningProcess = null;
                window.Invoke(() =>
                    window.SendWebMessage("{\"type\":\"game-exited\"}"));
            };

            // 轮询等待游戏窗口出现（最多 30 秒），窗口出现后才发送 game-launched
            _ = Task.Run(async () =>
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    while (sw.ElapsedMilliseconds < 30_000)
                    {
                        if (RunningProcess == null)
                        {
                            System.Diagnostics.Debug.WriteLine("[Launch] 轮询终止: RunningProcess 已被清空");
                            return;
                        }
                        if (processRef.Process?.HasExited == true)
                        {
                            // 退出事件处理器可能已接管（RunningProcess 已被清空），避免与 game-exited 重复报错
                            if (RunningProcess != processRef)
                            {
                                System.Diagnostics.Debug.WriteLine("[Launch] 轮询终止: 退出事件已处理");
                                return;
                            }
                            System.Diagnostics.Debug.WriteLine($"[MC] 启动失败，退出码: {processRef.Process.ExitCode}");
                            window.Invoke(() =>
                                window.SendWebMessage(JsonSerializer.Serialize(new { type = "game-error", message = "游戏启动失败，请检查版本完整性" })));
                            processRef.Dispose();
                            if (RunningProcess == processRef) RunningProcess = null;
                            return;
                        }
                        try
                        {
                            if (processRef.Process?.MainWindowHandle != IntPtr.Zero)
                            {
                                System.Diagnostics.Debug.WriteLine("[Launch] 游戏窗口已出现");
                                window.Invoke(() =>
                                    window.SendWebMessage("{\"type\":\"game-launched\"}"));
                                return;
                            }
                        }
                        catch { }
                        await Task.Delay(500);
                    }
                    System.Diagnostics.Debug.WriteLine("[Launch] 窗口检测超时，视为已启动");
                    window.Invoke(() =>
                        window.SendWebMessage("{\"type\":\"game-launched\"}"));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Launch] 状态检查异常: {ex.Message}");
                    window.Invoke(() =>
                        window.SendWebMessage(JsonSerializer.Serialize(new { type = "game-error", message = "游戏启动失败，请检查版本完整性" })));
                }
            });
        }
        catch (Exception ex)
        {
            window.Invoke(() =>
                window.SendWebMessage(JsonSerializer.Serialize(new { type = "game-error", message = ex.Message })));
        }
    }

    /// <summary>
    /// 启动被取消后的清理：释放进程并通知前端
    /// </summary>
    private static async Task CleanupCancelled(PhotinoWindow window)
    {
        MinecraftProcess? process = RunningProcess;
        RunningProcess = null;
        try
        {
            process?.Close();
            process?.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Launch] 取消清理失败: {ex.Message}");
        }
        await Task.CompletedTask;
        window.Invoke(() =>
            window.SendWebMessage("{\"type\":\"game-exited\"}"));
    }

    /// <summary>
    /// 手动构造启动参数（绕过ArgumentsParser的ParseJsonNode bug）
    /// </summary>
    private static List<string> BuildFallbackArgs(
        MinecraftEntry game, LaunchConfig config,
        MinecraftLaunch.Base.Models.Game.JavaEntry java, string minecraftPath)
    {
        using var versionJson = JsonDocument.Parse(File.ReadAllText(game.ClientJsonPath!));
        var root = versionJson.RootElement;

        var args = new List<string>();

        // 内存参数
        args.Add($"-Xms{config.MinMemorySize}M");
        args.Add($"-Xmx{config.MaxMemorySize}M");

        // JVM 参数：先读继承的 vanilla 版本，再读本版本
        if (game is ModifiedMinecraftEntry { HasInheritance: true } modded)
        {
            ReadJvmArgs(File.ReadAllText(modded.InheritedMinecraft.ClientJsonPath), args);
        }
        ReadJvmArgs(File.ReadAllText(game.ClientJsonPath!), args);

        // 用户自定义 JVM 参数
        if (config.JvmArguments != null)
            args.AddRange(config.JvmArguments);

        // 主类
        args.Add(root.GetProperty("mainClass").GetString()!);

        // 游戏参数：先读继承版本，再读本版本
        if (game is ModifiedMinecraftEntry { HasInheritance: true } modded2)
        {
            ReadGameArgs(File.ReadAllText(modded2.InheritedMinecraft.ClientJsonPath), args);
        }
        ReadGameArgs(File.ReadAllText(game.ClientJsonPath!), args);

        // 替换模板变量
        var classPath = BuildClassPath(game, minecraftPath);
        var versionName = game is ModifiedMinecraftEntry { HasInheritance: true } m
            ? m.InheritedMinecraft.Id : game.Id;

        var assetIndexPath = game is ModifiedMinecraftEntry { HasInheritance: true } m2
            ? m2.InheritedMinecraft.AssetIndexJsonPath
            : game.AssetIndexJsonPath;
        var assetIndexName = Path.GetFileNameWithoutExtension(assetIndexPath);

        var nativesDir = Path.Combine(minecraftPath, "versions", game.Id, "natives");

        var replacements = new Dictionary<string, string>
        {
            {"${launcher_name}", "DeciLauncher"},
            {"${launcher_version}", "1"},
            {"${classpath}", classPath},
            {"${classpath_separator}", OperatingSystem.IsWindows() ? ";" : ":"},
            {"${library_directory}", Path.Combine(minecraftPath, "libraries")},
            {"${libraries_directory}", Path.Combine(minecraftPath, "libraries")},
            {"${primary_jar}", game.ClientJarPath!},
            {"${version_name}", versionName},
            {"${natives_directory}", nativesDir},
            {"${auth_player_name}", config.Account!.Name},
            {"${auth_access_token}", config.Account!.AccessToken},
            {"${auth_uuid}", config.Account!.Uuid.ToString("N")},
            {"${auth_session}", config.Account!.AccessToken},
            {"${version_type}", "release"},
            {"${game_directory}", Path.Combine(minecraftPath, "versions", game.Id)},
            {"${game_assets}", Path.Combine(minecraftPath, "assets")},
            {"${assets_root}", Path.Combine(minecraftPath, "assets")},
            {"${assets_index_name}", assetIndexName ?? ""},
            {"${access_token}", config.Account!.AccessToken},
            {"${user_type}", "Mojang"},
            {"${user_properties}", "{}"},
            {"${clientid}", ""},
            {"${auth_xuid}", ""},
        };

        for (int i = 0; i < args.Count; i++)
        {
            foreach (var kv in replacements)
                args[i] = args[i].Replace(kv.Key, kv.Value);
        }

        // 校验残留模板变量（版本 JSON 若使用了未收录的 ${...} 会原样传给 JVM，报错晦涩）
        var leftovers = args.Where(a => a.Contains("${"));
        if (leftovers.Any())
            throw new InvalidOperationException($"未替换的模板变量: {string.Join(", ", leftovers)}");

        return args;
    }

    private static void ReadJvmArgs(string jsonContent, List<string> args)
    {
        try
        {
            using var json = JsonDocument.Parse(jsonContent);
            var root = json.RootElement;
            if (root.TryGetProperty("arguments", out var argsObj) && argsObj.TryGetProperty("jvm", out var jvmArr))
                AppendVersionArgs(args, jvmArr);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[WARN] ReadJvmArgs: {ex.Message}"); }
    }

    private static void ReadGameArgs(string jsonContent, List<string> args)
    {
        try
        {
            using var json = JsonDocument.Parse(jsonContent);
            var root = json.RootElement;
            if (root.TryGetProperty("arguments", out var argsObj) && argsObj.TryGetProperty("game", out var gameArr))
                AppendVersionArgs(args, gameArr);
            else if (root.TryGetProperty("minecraftArguments", out var mcArgs))
                args.AddRange(mcArgs.GetString()!.Split(' '));
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[WARN] ReadGameArgs: {ex.Message}"); }
    }

    private static void AppendVersionArgs(List<string> args, JsonElement arr)
    {
        foreach (var arg in arr.EnumerateArray())
        {
            if (arg.ValueKind == JsonValueKind.String)
            {
                args.Add(arg.GetString()!);
            }
            else if (arg.ValueKind == JsonValueKind.Object &&
                     arg.TryGetProperty("value", out var val) &&
                     (!arg.TryGetProperty("rules", out var rules) || RulesAllow(rules)))
            {
                if (val.ValueKind == JsonValueKind.String)
                    args.Add(val.GetString()!);
                else if (val.ValueKind == JsonValueKind.Array)
                    foreach (var item in val.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String)
                            args.Add(item.GetString()!);
            }
        }
    }

    private static string BuildClassPath(MinecraftEntry game, string minecraftPath)
    {
        var parts = new List<string>();

        // 核心 jar
        if (game.ClientJarPath is not null)
            parts.Add(game.ClientJarPath);

        // 从版本 JSON 中解析库路径（不触发 ParseJsonNode bug）
        var libPaths = ParseLibraryPaths(game, minecraftPath);
        parts.AddRange(libPaths.Select(p => Path.Combine(minecraftPath, "libraries", p)));

        return string.Join(OperatingSystem.IsWindows() ? ';' : ':', parts);
    }

    /// <summary>
    /// 从版本 JSON 的 libraries 字段提取每个库的文件路径
    /// 对 ModdedEntry 同时读取继承的 vanilla 版本 JSON
    /// </summary>
    private static List<string> ParseLibraryPaths(MinecraftEntry game, string minecraftPath)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>();

        // 先读继承的 vanilla 库（如果是模组版）
        if (game is ModifiedMinecraftEntry { HasInheritance: true } modded)
        {
            ReadLibraryPathsFromJson(modded.InheritedMinecraft.ClientJsonPath, paths, seen);
        }

        // 再读本版本库
        ReadLibraryPathsFromJson(game.ClientJsonPath!, paths, seen);

        return paths;
    }

    private static void ReadLibraryPathsFromJson(string jsonPath, List<string> paths, HashSet<string> seen)
    {
        if (!File.Exists(jsonPath)) return;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(jsonPath));
            if (!json.RootElement.TryGetProperty("libraries", out var libs)) return;

            foreach (var lib in libs.EnumerateArray())
            {
                string? path = null;
                // 优先从 downloads.artifact.path 获取
                if (lib.TryGetProperty("downloads", out var downloads) &&
                    downloads.TryGetProperty("artifact", out var artifact) &&
                    artifact.TryGetProperty("path", out var p))
                {
                    path = p.GetString();
                }
                // 回退：从 name 构造路径
                if (path == null && lib.TryGetProperty("name", out var name))
                {
                    path = LibraryNameToPath(name.GetString()!);
                }

                if (path != null && seen.Add(path))
                    paths.Add(path);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WARN] ReadLibraryPaths: {ex.Message}");
        }
    }

    /// <summary>
    /// 将 Maven 库名（如 net.fabricmc:fabric-loader:0.19.3）转换为文件路径
    /// </summary>
    private static string LibraryNameToPath(string name)
    {
        var parts = name.Split(':');
        if (parts.Length < 3) return name + ".jar";
        var group = parts[0].Replace('.', '/');
        var artifact = parts[1];
        var version = parts[2];
        return $"{group}/{artifact}/{version}/{artifact}-{version}.jar";
    }

    private static bool RulesAllow(JsonElement rules)
    {
        var currentOs = OperatingSystem.IsWindows() ? "windows"
            : OperatingSystem.IsMacOS() ? "osx" : "linux";
        bool allowed = false;
        foreach (var rule in rules.EnumerateArray())
        {
            // features 规则由启动器显式控制，不自动启用
            if (rule.TryGetProperty("features"u8, out _))
                continue;
            if (rule.TryGetProperty("os", out var os))
            {
                if (os.TryGetProperty("name", out var name) &&
                    !string.Equals(name.GetString(), currentOs, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            if (rule.TryGetProperty("action", out var action))
            {
                if (action.ValueEquals("allow"u8))
                    allowed = true;
                else if (action.ValueEquals("disallow"u8))
                    allowed = false;
            }
        }
        return allowed;
    }
}
