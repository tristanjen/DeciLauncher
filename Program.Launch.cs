// 离线账户认证
using MinecraftLaunch.Components.Authenticator;
// .minecraft 解析 + 启动
using MinecraftLaunch.Components.Parser;
using MinecraftLaunch.Launch;
// 游戏/Java 数据模型
using MinecraftLaunch.Base.Models.Game;
// Account 类型（认证缓存）
using MinecraftLaunch.Base.Models.Authentication;
// Java 自动检测 + 版本匹配
using MinecraftLaunch.Extensions;
using MinecraftLaunch.Utilities;
// JSON 解析（AOT 安全的 JsonDocument）
using System.Text.Json;
// native 库解压（fallback 路径手动解压 natives）
using System.IO.Compression;
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
    /// 启动防重入标志：1 = 有启动流程在执行。
    /// Interlocked 检查-设置，覆盖「RunningProcess 检查」与「RunAsync 赋值」之间的 await 窗口
    /// </summary>
    private static int LaunchActive;
    /// <summary>
    /// 启动代次：每次 launch-game 递增。迟到的异步回调（取消异常、轮询任务）
    /// 仅在代次仍为当前值时通知前端，避免复位下一次启动的 UI 状态
    /// </summary>
    private static int LaunchGeneration;

    /// <summary>
    /// 关闭正在运行的游戏（同时供 CancelLaunch 复用）。
    /// 即使后端已无进程引用也回发 game-exited：前端状态可能因消息乱序
    /// （game-launched 晚于 game-exited 到达）而失配，需要一条复位消息恢复
    /// </summary>
    private static void CloseGame(PhotinoWindow window)
    {
        if (RunningProcess == null)
        {
            TryNotifyWindow(window, "{\"type\":\"game-exited\"}");
            return;
        }
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
        TryNotifyWindow(window, "{\"type\":\"game-exited\"}");
    }

    /// <summary>
    /// 取消正在进行的游戏启动（取消 RunAsync + 关闭已启动的进程）
    /// </summary>
    private static void CancelLaunch(PhotinoWindow window)
    {
        // 捕获当前 CTS 局部引用再取消，避免与下一次启动重建的 LaunchCts 产生竞态
        var cts = Volatile.Read(ref LaunchCts);
        cts.Cancel();

        if (RunningProcess != null)
        {
            // 已有进程在运行：关闭并复位前端
            CloseGame(window);
            return;
        }

        // 无运行进程：
        // - 有进行中的启动任务时，其取消路径（CleanupCancelled）会按代次发送 game-exited，此处不再发送，避免双发；
        // - 无进行中任务（LaunchActive == 0）说明前端状态失配（如复位消息丢失），补发一条复位消息
        if (Volatile.Read(ref LaunchActive) == 0)
            TryNotifyWindow(window, "{\"type\":\"game-exited\"}");
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
        // 防重入：检查-设置原子完成，覆盖「RunningProcess 检查」与「RunAsync 赋值」之间的 await 窗口，
        // 两条并发的 launch-game 消息只有第一条能进入启动流程
        if (Interlocked.CompareExchange(ref LaunchActive, 1, 0) != 0)
        {
            TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = L("已有游戏正在启动", "A game is already launching") }));
            return;
        }
        // 本次启动的代次：迟到的取消回调/轮询任务仅在仍是当前代次时通知前端
        var generation = Interlocked.Increment(ref LaunchGeneration);
        bool IsCurrent() => generation == Volatile.Read(ref LaunchGeneration);
        try
        {
            System.Diagnostics.Debug.WriteLine($"[Launch] 游戏: {gameId}");
            System.Diagnostics.Debug.WriteLine($"[Launch] 账户 UUID: {accountUuid}");
            System.Diagnostics.Debug.WriteLine($"[Launch] Java: {javaPath}");
            System.Diagnostics.Debug.WriteLine($"[Launch] 内存: {maxMemory} MB");
            System.Diagnostics.Debug.WriteLine($"[Launch] 路径: {minecraftPath}");

            // 并发防护：已有游戏进程在运行时拒绝重复启动，避免两个启动任务互相覆盖 RunningProcess
            if (RunningProcess != null)
            {
                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = L("已有游戏正在运行", "A game is already running") }));
                return;
            }

            // 重建取消令牌（上次启动可能已取消；Volatile 保证与 CancelLaunch/close 处理器的可见性）
            Volatile.Write(ref LaunchCts, new CancellationTokenSource());
            var launchToken = Volatile.Read(ref LaunchCts).Token;

            if (maxMemory < 512) maxMemory = 512;
            if (maxMemory > 16384) maxMemory = 16384;

            // 1. 查找账户（锁内读取，避免与账户删除并发破坏列表/字典；优先使用缓存，避免每次重新 Authenticate 导致 UUID 不一致。
            // 消息发送移到锁外，避免持锁等待 UI 线程）
            AccountEntry? accountEntry;
            Account? account = null;
            lock (AccountsLock)
            {
                accountEntry = Accounts.FirstOrDefault(a => a.Uuid == accountUuid);
                if (accountEntry != null && !AuthenticatedAccounts.TryGetValue(accountEntry.Uuid, out account))
                {
                    account = new OfflineAuthenticator().Authenticate(accountEntry.Username);
                    AuthenticatedAccounts[accountEntry.Uuid] = account;
                }
            }
            if (accountEntry == null)
            {
                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = L("未找到选中的账户", "Selected account not found") }));
                return;
            }
            if (launchToken.IsCancellationRequested) { CleanupCancelled(window, null, generation); return; }

            // 2. 解析游戏版本
            var parser = new MinecraftParser(minecraftPath);
            var game = parser.GetMinecraft(gameId);
            if (game == null)
            {
                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = L("未找到选中的游戏版本", "Selected game version not found") }));
                return;
            }
            if (launchToken.IsCancellationRequested) { CleanupCancelled(window, null, generation); return; }

            // 3. 查找 Java 运行时（后台线程执行，避免阻塞 UI）
            var javas = await Task.Run(() => JavaUtil.EnumerableJavaAsync().ToBlockingEnumerable().ToList(), launchToken);
            if (launchToken.IsCancellationRequested) { CleanupCancelled(window, null, generation); return; }
            MinecraftLaunch.Base.Models.Game.JavaEntry? java = javaPath switch
            {
                "__auto__" or "" => game.GetAppropriateJava(javas),
                _ => javas.FirstOrDefault(j => j.JavaPath == javaPath)
            };
            if (java == null)
            {
                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = L("未找到合适的 Java 运行时", "No suitable Java runtime found") }));
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
            // 注意：不设置 NativesFolder——MinecraftLaunch 4.0.7 的 MinecraftRunner 仅在
            // NativesFolder 为空时执行 natives 解压，且 ExtractNatives 硬编码解压到
            // versions/<id>/natives；设置非空值会导致 native 库从未解压且 JVM 参数指向空目录
            var config = new LaunchConfig
            {
                Account = account,
                MaxMemorySize = maxMemory,
                MinMemorySize = 512,
                JavaPath = java,
                LauncherName = "DeciLauncher",
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
                CleanupCancelled(window, null, generation);
                return;
            }
            catch (Exception rex)
            {
                System.Diagnostics.Debug.WriteLine($"[Launch] RunAsync 异常: {rex}");
                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = $"{L("启动异常", "Launch error")}: {rex.Message}" }));
                return;
            }
            if (launchToken.IsCancellationRequested)
            {
                CleanupCancelled(window, RunningProcess, generation);
                return;
            }

            // RunAsync 可能返回 null（库内部提前返回），此时独立构造路径无法工作，直接报错
            if (RunningProcess == null)
            {
                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = L("启动失败：未创建游戏进程", "Launch failed: no game process was created") }));
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

                    // 确保 native 库已解压（RunAsync 内可能因异常提前返回未执行解压；幂等）
                    ExtractNativesFallback(game, minecraftPath);

                    var proc = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo(java.JavaPath)
                        {
                            // 逐参数加引号，避免含空格路径（classpath/natives 等）被拆成多个参数
                            Arguments = JoinArguments(arguments),
                            WorkingDirectory = Path.Combine(minecraftPath, "versions", gameId),
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        },
                        EnableRaisingEvents = true
                    };
                    var procProp = typeof(MinecraftProcess).GetProperty("Process");
                    procProp?.SetValue(RunningProcess, proc);
                    if (launchToken.IsCancellationRequested) { CleanupCancelled(window, RunningProcess, generation); return; }
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

                        // 确保 native 库已解压到 versions/<id>/natives（fallback 的 natives_directory 指向该处）
                        ExtractNativesFallback(game, minecraftPath);

                        var proc = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo(java.JavaPath)
                            {
                                // 逐参数加引号，避免含空格路径（classpath/natives 等）被拆成多个参数
                                Arguments = JoinArguments(fallbackArgs),
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

                        // 手动绑定退出事件（MinecraftProcess 内置回调在构造器提前 return 后未绑定）
                        var mpRef = RunningProcess;
                        proc.Exited += (_, _) =>
                        {
                            System.Diagnostics.Debug.WriteLine("[MC] 游戏进程已退出（fallback 路径）");
                            proc.Dispose();
                            // 与正常路径一致：仅当仍是被跟踪的进程时才清理引用并通知前端，
                            // 防止旧进程迟到的退出事件清掉下一次启动的进程引用
                            if (ReferenceEquals(RunningProcess, mpRef))
                            {
                                RunningProcess = null;
                                TryNotifyWindow(window, "{\"type\":\"game-exited\"}");
                            }
                        };

                        if (launchToken.IsCancellationRequested) { CleanupCancelled(window, RunningProcess, generation); return; }
                        // MinecraftProcess.Start() 内部已执行 Process.Start() + BeginOutputReadLine/BeginErrorReadLine，
                        // 此处不得重复调用（否则抛 async read already started）
                        RunningProcess.Start();
                    }
                    catch (Exception fallbackEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Launch] 手动启动失败: {fallbackEx}");
                        TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = $"{L("启动失败", "Launch failed")}: {fallbackEx.Message}" }));
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
                // 仅当仍是被跟踪的进程时才清理引用并通知前端：
                // 用户关闭游戏时 CloseGame 已清空引用并发送 game-exited，
                // 迟到的退出事件不得误伤下一次启动的进程引用或重复复位
                if (RunningProcess == processRef)
                {
                    RunningProcess = null;
                    TryNotifyWindow(window, "{\"type\":\"game-exited\"}");
                }
            };

            // 轮询等待游戏窗口出现（最多 30 秒），窗口出现后才发送 game-launched
            _ = Task.Run(async () =>
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    while (sw.ElapsedMilliseconds < 30_000)
                    {
                        // 本次启动已被更新的启动取代（代次变化）或进程引用已被清空时终止轮询
                        if (!IsCurrent() || RunningProcess == null)
                        {
                            System.Diagnostics.Debug.WriteLine("[Launch] 轮询终止: 启动已被替代或 RunningProcess 已被清空");
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
                            TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = L("游戏启动失败，请检查版本完整性", "Game failed to start. Please check version integrity.") }));
                            processRef.Dispose();
                            if (RunningProcess == processRef) RunningProcess = null;
                            return;
                        }
                        try
                        {
                            if (processRef.Process?.MainWindowHandle != IntPtr.Zero)
                            {
                                if (!IsCurrent())
                                {
                                    System.Diagnostics.Debug.WriteLine("[Launch] 轮询终止: 窗口出现前启动已被替代");
                                    return;
                                }
                                System.Diagnostics.Debug.WriteLine("[Launch] 游戏窗口已出现");
                                TryNotifyWindow(window, "{\"type\":\"game-launched\"}");
                                return;
                            }
                        }
                        catch { }
                        await Task.Delay(500);
                    }
                    // 超时视为已启动，但发送前需确认本次启动仍是最新且进程引用未被清空，
                    // 防止与退出事件竞态导致 game-launched 晚于 game-exited 到达前端（UI 永久卡在「运行中」）
                    if (!IsCurrent() || RunningProcess != processRef)
                    {
                        System.Diagnostics.Debug.WriteLine("[Launch] 窗口检测超时，但启动已被替代，不发送 game-launched");
                        return;
                    }
                    System.Diagnostics.Debug.WriteLine("[Launch] 窗口检测超时，视为已启动");
                    TryNotifyWindow(window, "{\"type\":\"game-launched\"}");
                }
                catch (Exception ex)
                {
                    // 代次守卫：启动已被替代时，迟到的轮询异常不得误报给前端
                    if (!IsCurrent())
                    {
                        System.Diagnostics.Debug.WriteLine("[Launch] 轮询异常但启动已被替代，忽略");
                        return;
                    }
                    System.Diagnostics.Debug.WriteLine($"[Launch] 状态检查异常: {ex.Message}");
                    TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = L("游戏启动失败，请检查版本完整性", "Game failed to start. Please check version integrity.") }));
                }
            });
        }
        catch (OperationCanceledException)
        {
            // 取消（如 Java 枚举的 await 直接抛 OCE）不是启动失败，走取消清理路径而非报 game-error
            System.Diagnostics.Debug.WriteLine("[Launch] 启动已取消（外层）");
            CleanupCancelled(window, RunningProcess, generation);
        }
        catch (Exception ex)
        {
            TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = ex.Message }));
        }
        finally
        {
            // 无论成功、失败还是取消，释放防重入标志，允许下一次启动
            Volatile.Write(ref LaunchActive, 0);
        }
    }

    /// <summary>
    /// 启动被取消后的清理：仅释放本次启动创建的进程（按引用相等判断，避免误杀下一次启动的进程）并通知前端。
    /// game-exited 仅在本次启动仍是当前代次时发送：迟到的取消回调不应复位下一次启动的 UI 状态
    /// </summary>
    private static void CleanupCancelled(PhotinoWindow window, MinecraftProcess? process, int generation)
    {
        try
        {
            if (process != null && ReferenceEquals(RunningProcess, process))
            {
                RunningProcess = null;
                process.Close();
                process.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Launch] 取消清理失败: {ex.Message}");
        }
        if (generation == Volatile.Read(ref LaunchGeneration))
            TryNotifyWindow(window, "{\"type\":\"game-exited\"}");
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
        // 文件缺失时跳过该来源（损坏安装不应让整个 fallback 失败，与 ParseLibraryPaths/ExtractNativesFallback 一致）
        if (game is ModifiedMinecraftEntry { HasInheritance: true } modded &&
            modded.InheritedMinecraft.ClientJsonPath is { } inheritedJson && File.Exists(inheritedJson))
        {
            ReadJvmArgs(File.ReadAllText(inheritedJson), args);
        }
        if (game.ClientJsonPath is { } clientJson && File.Exists(clientJson))
            ReadJvmArgs(File.ReadAllText(clientJson), args);

        // 用户自定义 JVM 参数
        if (config.JvmArguments != null)
            args.AddRange(config.JvmArguments);

        // 主类
        args.Add(root.GetProperty("mainClass").GetString()!);

        // 游戏参数：先读继承版本，再读本版本（文件缺失时跳过，同上）
        if (game is ModifiedMinecraftEntry { HasInheritance: true } modded2 &&
            modded2.InheritedMinecraft.ClientJsonPath is { } inheritedJson2 && File.Exists(inheritedJson2))
        {
            ReadGameArgs(File.ReadAllText(inheritedJson2), args);
        }
        if (game.ClientJsonPath is { } clientJson2 && File.Exists(clientJson2))
            ReadGameArgs(File.ReadAllText(clientJson2), args);

        // 替换模板变量
        var classPath = BuildClassPath(game, minecraftPath);
        // ${version_name} 与 MinecraftLaunch 一致使用当前启动的版本 Id（而非继承的 vanilla Id）
        var versionName = game.Id;

        var assetIndexPath = game is ModifiedMinecraftEntry { HasInheritance: true } m2
            ? m2.InheritedMinecraft.AssetIndexJsonPath
            : game.AssetIndexJsonPath;
        var assetIndexName = Path.GetFileNameWithoutExtension(assetIndexPath);

        // 版本类型：从版本 JSON 顶层 "type" 字段读取（release/snapshot/old_beta/old_alpha），缺失时回退 "release"
        var versionType = root.TryGetProperty("type", out var vt) ? vt.GetString() ?? "release" : "release";

        // natives 目录与 MinecraftLaunch ExtractNatives 的硬编码解压目标保持一致
        // （versions/<id>/natives），启动前由 ExtractNativesFallback 确保已解压
        var nativesDir = Path.Combine(minecraftPath, "versions", game.Id, "natives");
        Directory.CreateDirectory(nativesDir);

        // primary jar：ModdedEntry 自身目录通常没有 jar，回退到继承的 vanilla client jar
        var primaryJar = game.ClientJarPath
            ?? (game is ModifiedMinecraftEntry { HasInheritance: true } mm
                ? mm.InheritedMinecraft.ClientJarPath
                : "");

        var replacements = new Dictionary<string, string>
        {
            {"${launcher_name}", "DeciLauncher"},
            {"${launcher_version}", "1"},
            {"${classpath}", classPath},
            {"${classpath_separator}", OperatingSystem.IsWindows() ? ";" : ":"},
            {"${library_directory}", Path.Combine(minecraftPath, "libraries")},
            {"${libraries_directory}", Path.Combine(minecraftPath, "libraries")},
            {"${primary_jar}", primaryJar},
            {"${version_name}", versionName},
            {"${natives_directory}", nativesDir},
            {"${auth_player_name}", config.Account!.Name},
            {"${auth_access_token}", config.Account!.AccessToken},
            {"${auth_uuid}", config.Account!.Uuid.ToString("N")},
            {"${auth_session}", config.Account!.AccessToken},
            {"${version_type}", versionType},
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
                // 旧版单行参数：引号感知拆分（含引号的 token 不按空格拆）
                args.AddRange(SplitArgsRespectingQuotes(mcArgs.GetString() ?? ""));
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
        var seen = new HashSet<string>();

        // 模组版：先加继承的 vanilla client jar（Fabric/Quilt 自身目录通常没有 jar）
        if (game is ModifiedMinecraftEntry { HasInheritance: true } modded &&
            modded.InheritedMinecraft.ClientJarPath is { } inheritedJar && seen.Add(inheritedJar))
            parts.Add(inheritedJar);

        // 核心 jar（存在且未重复时）
        if (game.ClientJarPath is { } jar && seen.Add(jar))
            parts.Add(jar);

        // 从版本 JSON 中解析库路径（不触发 ParseJsonNode bug）
        var libPaths = ParseLibraryPaths(game, minecraftPath);
        foreach (var p in libPaths)
        {
            var full = Path.Combine(minecraftPath, "libraries", p);
            if (seen.Add(full))
                parts.Add(full);
        }

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
    /// fallback/独立路径启动前确保 native 库已解压到 versions/<id>/natives
    /// （与 MinecraftLaunch ExtractNatives 的硬编码目标一致；幂等：已存在的文件跳过）。
    /// 从版本 JSON（含继承版本）的 libraries 中筛选 natives 库，覆盖两种 Mojang 格式：
    /// - 旧格式（&lt;1.20.5）：libraries[i].natives 对象的键是 OS 名（windows/osx/linux），
    ///   值才是 classifier（natives-windows / natives-macos 等），文件位于 downloads.classifiers[classifier].path；
    /// - 新格式（1.20.5+）：natives 库为独立条目，classifier 在 name 的第 4 段
    ///   （如 org.lwjgl:lwjgl:3.3.3:natives-windows），文件位于 downloads.artifact.path。
    /// 按当前进程架构过滤（arm64 只取 -arm64 变体），避免 x64/arm64 文件混入同一目录；
    /// macOS 同时接受 1.13 前的 natives-osx 命名。
    /// </summary>
    private static void ExtractNativesFallback(MinecraftEntry game, string minecraftPath)
    {
        try
        {
            var nativesDir = Path.Combine(minecraftPath, "versions", game.Id, "natives");
            Directory.CreateDirectory(nativesDir);

            var platformPrefix = OperatingSystem.IsWindows() ? "natives-windows"
                : OperatingSystem.IsMacOS() ? "natives-macos" : "natives-linux";
            var ext = OperatingSystem.IsWindows() ? ".dll"
                : OperatingSystem.IsMacOS() ? ".dylib" : ".so";

            // 架构后缀：优先解压与当前进程架构精确匹配的 classifier；
            // arm64/x86 机器上老版本没有对应变体时，回退解压无后缀/x64 变体（兼容/仿真运行）
            var archSuffix = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
            {
                System.Runtime.InteropServices.Architecture.Arm64 => "-arm64",
                System.Runtime.InteropServices.Architecture.X86 => "-x86",
                _ => "" // x64 等使用无后缀 classifier（natives-windows）
            };

            bool MatchesPlatform(string classifier, bool allowX64Fallback)
            {
                // macOS：1.13 之前 Mojang 使用 natives-osx，之后改用 natives-macos，两者都接受
                string? matchedPrefix = null;
                if (classifier.StartsWith(platformPrefix, StringComparison.OrdinalIgnoreCase))
                    matchedPrefix = platformPrefix;
                else if (OperatingSystem.IsMacOS() &&
                         classifier.StartsWith("natives-osx", StringComparison.OrdinalIgnoreCase))
                    matchedPrefix = "natives-osx";
                if (matchedPrefix == null) return false;

                var rest = classifier.Substring(matchedPrefix.Length);
                if (rest.Length == 0)
                    return archSuffix.Length == 0 || allowX64Fallback;
                if (rest.Equals(archSuffix, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (rest.Equals("-x64", StringComparison.OrdinalIgnoreCase) ||
                    rest.Equals("-x86_64", StringComparison.OrdinalIgnoreCase) ||
                    rest.Equals("-64", StringComparison.OrdinalIgnoreCase))
                    // x86 进程无法加载 x64 变体，回退阶段也不接受
                    return archSuffix.Length == 0 || (allowX64Fallback && archSuffix != "-x86");
                // x86 进程兼容旧命名 natives-*-32
                return archSuffix == "-x86" && rest.Equals("-32", StringComparison.OrdinalIgnoreCase);
            }

            var jsonPaths = new List<string>();
            if (game is ModifiedMinecraftEntry { HasInheritance: true } modded)
                jsonPaths.Add(modded.InheritedMinecraft.ClientJsonPath);
            jsonPaths.Add(game.ClientJsonPath!);

            // 严格阶段已处理（解压成功）的库 base 名（Maven 名前 3 段）。
            // 回退决策是库粒度而非全局计数：同一版本中可能同时存在「有 arm64 变体的库」
            // 与「仅有 x64 变体的库」（如官方 lwjgl vs 模组 natives），
            // 全局计数会让回退阶段整体跳过，导致仅 x64 变体的库缺失 natives
            var handledBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 两阶段：先严格匹配当前架构；回退阶段对严格阶段未处理的库解压 x64 变体
            foreach (var allowX64Fallback in new[] { false, true })
            {
                foreach (var jsonPath in jsonPaths)
                {
                    if (!File.Exists(jsonPath)) continue;
                    using var json = JsonDocument.Parse(File.ReadAllText(jsonPath));
                    if (!json.RootElement.TryGetProperty("libraries", out var libs)) continue;

                    foreach (var lib in libs.EnumerateArray())
                    {
                        var libBase = GetLibBase(lib);
                        // 严格阶段已解压同 base 的变体（如 arm64），回退阶段跳过，
                        // 避免 x64/arm64 文件混入同一 natives 目录
                        if (allowX64Fallback && libBase.Length > 0 && handledBases.Contains(libBase))
                            continue;

                        if (lib.TryGetProperty("natives", out var nativesObj))
                        {
                            // 旧格式（&lt;1.20.5）：natives 对象的键是 OS 名（windows/osx/linux），
                            // 值才是 classifier（natives-windows 等）——按值匹配平台前缀，
                            // 文件位于 downloads.classifiers[classifier].path
                            foreach (var prop in nativesObj.EnumerateObject())
                            {
                                var classifier = prop.Value.GetString();
                                if (string.IsNullOrEmpty(classifier) || !MatchesPlatform(classifier, allowX64Fallback))
                                    continue;
                                if (ExtractFromClassifier(lib, classifier, minecraftPath, nativesDir, ext) &&
                                    libBase.Length > 0)
                                    handledBases.Add(libBase);
                            }
                            continue;
                        }

                        // 新格式（1.20.5+）：natives 库为独立条目，classifier 在 name 的第 4 段
                        // （如 org.lwjgl:lwjgl:3.3.3:natives-windows），文件位于 downloads.artifact.path
                        if (lib.TryGetProperty("name", out var nameElem) &&
                            nameElem.GetString() is { } mavenName)
                        {
                            var nameParts = mavenName.Split(':');
                            if (nameParts.Length >= 4 && MatchesPlatform(nameParts[3], allowX64Fallback) &&
                                ExtractFromArtifact(lib, minecraftPath, nativesDir, ext) &&
                                libBase.Length > 0)
                                handledBases.Add(libBase);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 解压失败不致命：游戏启动后会给出更明确的 LWJGL 错误
            System.Diagnostics.Debug.WriteLine($"[WARN] ExtractNativesFallback 失败: {ex.Message}");
        }

        // 库的 base 标识：Maven 名前 3 段（去除 classifier 段），跨旧/新格式一致
        static string GetLibBase(JsonElement lib)
        {
            if (lib.TryGetProperty("name", out var nameElem) &&
                nameElem.GetString() is { } name)
            {
                var parts = name.Split(':');
                return parts.Length >= 3 ? string.Join(':', parts.Take(3)) : name;
            }
            return "";
        }

        // 旧格式：从 downloads.classifiers[classifier].path 定位并解压
        static bool ExtractFromClassifier(JsonElement lib, string classifier, string minecraftPath, string nativesDir, string ext)
        {
            if (!lib.TryGetProperty("downloads", out var downloads) ||
                !downloads.TryGetProperty("classifiers", out var classifiers) ||
                !classifiers.TryGetProperty(classifier, out var artifact) ||
                !artifact.TryGetProperty("path", out var pathProp))
                return false;

            return ExtractZipToNatives(Path.Combine(minecraftPath, "libraries", pathProp.GetString()!), nativesDir, ext);
        }

        // 新格式（1.20.5+）：从 downloads.artifact.path 定位并解压
        static bool ExtractFromArtifact(JsonElement lib, string minecraftPath, string nativesDir, string ext)
        {
            if (!lib.TryGetProperty("downloads", out var downloads) ||
                !downloads.TryGetProperty("artifact", out var artifact) ||
                !artifact.TryGetProperty("path", out var pathProp))
                return false;

            return ExtractZipToNatives(Path.Combine(minecraftPath, "libraries", pathProp.GetString()!), nativesDir, ext);
        }

        // 将 zip 中指定扩展名的文件解压到 natives 目录（幂等：已存在的文件跳过）。
        // 返回是否实际处理了该库文件（存在即 true，用于两阶段回退的产出计数）
        static bool ExtractZipToNatives(string zipPath, string nativesDir, string ext)
        {
            if (!File.Exists(zipPath)) return false;

            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var entry in zip.Entries)
            {
                if (!string.Equals(Path.GetExtension(entry.FullName), ext, StringComparison.OrdinalIgnoreCase))
                    continue;
                var target = Path.Combine(nativesDir, Path.GetFileName(entry.FullName));
                if (!File.Exists(target))
                {
                    entry.ExtractToFile(target, true);
                    System.Diagnostics.Debug.WriteLine($"[Launch] 解压 native: {target}");
                }
            }
            return true;
        }
    }

    /// <summary>
    /// 将 Maven 库名转换为文件路径。
    /// 支持带 classifier 的四段名（如 org.lwjgl:lwjgl:3.2.1:natives-windows）
    /// → <artifact>-<version>-<classifier>.jar
    /// </summary>
    private static string LibraryNameToPath(string name)
    {
        var parts = name.Split(':');
        if (parts.Length < 3) return name + ".jar";
        var group = parts[0].Replace('.', '/');
        var artifact = parts[1];
        var version = parts[2];
        if (parts.Length >= 4)
            return $"{group}/{artifact}/{version}/{artifact}-{version}-{string.Join('-', parts.Skip(3))}.jar";
        return $"{group}/{artifact}/{version}/{artifact}-{version}.jar";
    }

    private static bool RulesAllow(JsonElement rules)
    {
        var currentOs = OperatingSystem.IsWindows() ? "windows"
            : OperatingSystem.IsMacOS() ? "osx" : "linux";
        // 当前进程架构映射为版本 JSON 的 arch 取值（x86/x64/arm64）
        var currentArch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X86 => "x86",
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.Arm => "arm",
            _ => ""
        };
        bool allowed = false;
        foreach (var rule in rules.EnumerateArray())
        {
            // features 规则由启动器显式控制，不自动启用
            if (rule.TryGetProperty("features"u8, out _))
                continue;
            // os 规则：name 不匹配当前系统时跳过该规则
            if (rule.TryGetProperty("os", out var os))
            {
                if (os.TryGetProperty("name", out var name) &&
                    !string.Equals(name.GetString(), currentOs, StringComparison.OrdinalIgnoreCase))
                    continue;
                // arch 规则：不匹配当前架构时跳过该规则（如 x86-only 的库在 x64/arm64 上不应生效）
                if (os.TryGetProperty("arch", out var arch) &&
                    !string.Equals(arch.GetString(), currentArch, StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// 按 MSVCRT 命令行规则将参数列表拼接为单个命令行字符串：
    /// 含空格的参数用双引号包裹，并正确转义反斜杠与引号，
    /// 避免含空格路径（classpath/natives/game_directory 等）被 JVM 拆成多个参数
    /// </summary>
    private static string JoinArguments(IEnumerable<string> arguments) =>
        string.Join(' ', arguments.Select(QuoteArgument));

    /// <summary>
    /// 单个参数的 MSVCRT 引号规则：无空白字符则原样返回；
    /// 否则用双引号包裹，并把尾随反斜杠与内嵌引号按 n*2+1 规则转义
    /// </summary>
    private static string QuoteArgument(string arg)
    {
        if (arg.Length == 0 || !arg.Any(char.IsWhiteSpace))
            return arg;

        var sb = new System.Text.StringBuilder(arg.Length + 2);
        sb.Append('"');
        int backslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }
            if (c == '"')
            {
                sb.Append('\\', backslashes * 2 + 1);
                sb.Append('"');
                backslashes = 0;
                continue;
            }
            sb.Append('\\', backslashes);
            backslashes = 0;
            sb.Append(c);
        }
        sb.Append('\\', backslashes * 2);
        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>
    /// 引号感知的空白拆分：旧版 minecraftArguments 单行字符串中，
    /// 双引号包裹的 token 内部空格不参与拆分，引号本身被剥除（由 JoinArguments 重新加回）
    /// </summary>
    private static IEnumerable<string> SplitArgsRespectingQuotes(string input)
    {
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (var ch in input)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                continue;
            }
            current.Append(ch);
        }
        if (current.Length > 0)
            yield return current.ToString();
    }

    /// <summary>
    /// 向前端发送消息的安全包装：窗口关闭后 Photino 的 Invoke/SendWebMessage 可能抛异常，
    /// 统一吞掉并记录，避免 async void / 后台任务因未处理异常导致进程崩溃
    /// </summary>
    private static void TryNotifyWindow(PhotinoWindow window, string message)
    {
        try
        {
            window.Invoke(() => window.SendWebMessage(message));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Window] 消息发送失败（窗口可能已关闭）: {ex.Message}");
        }
    }
}
