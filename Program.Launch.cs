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
            TryNotifyWindow(window, GameMessages.GameExited);
            return;
        }
        try
        {
            RunningProcess.Close();
            RunningProcess.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug($"[Launch] 关闭游戏失败: {ex.Message}");
        }
        RunningProcess = null;
        TryNotifyWindow(window, GameMessages.GameExited);
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
            TryNotifyWindow(window, GameMessages.GameExited);
    }

    /// <summary>
    /// 执行游戏启动流程：查找账户 → 解析游戏 → 匹配 Java → 启动
    /// 返回 Task 而非 async void：异常由内部 try/catch 全量捕获并转 game-error 消息，
    /// 调用方以 fire-and-forget（_ = ...）方式投递，未处理异常不会逃逸到消息线程
    /// </summary>
    private static async Task LaunchGameAsync(
        PhotinoWindow window,
        string gameId, string accountUuid,
        string javaPath, int maxMemory,
        string minecraftPath)
    {
        // 安全校验：gameId 仅允许版本目录名（拒绝路径分隔符与 .. 防路径逃逸）。
        // 必须在防重入检查之前：校验失败直接返回，此时 LaunchActive 尚未置位，
        // 否则 LaunchActive 只能在 finally 复位而此路径不会进入 try
        if (string.IsNullOrEmpty(gameId) ||
            gameId.Contains('/') || gameId.Contains('\\') || gameId.Contains(".."))
        {
            TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = L("无效的游戏版本 ID", "Invalid game version ID") }));
            return;
        }

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
            Log.Debug($"[Launch] 游戏: {gameId}");
            Log.Debug($"[Launch] 账户 UUID: {accountUuid}");
            Log.Debug($"[Launch] Java: {javaPath}");
            Log.Debug($"[Launch] 内存: {maxMemory} MB");
            Log.Debug($"[Launch] 路径: {minecraftPath}");

            // 并发防护：已有游戏进程在运行时拒绝重复启动，避免两个启动任务互相覆盖 RunningProcess
            if (RunningProcess != null)
            {
                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = L("已有游戏正在运行", "A game is already running") }));
                return;
            }

            // 重建取消令牌（上次启动可能已取消；Volatile 保证与 CancelLaunch/close 处理器的可见性）
            Volatile.Write(ref LaunchCts, new CancellationTokenSource());
            var launchToken = Volatile.Read(ref LaunchCts).Token;

            // 本次启动的开始时间（崩溃分析据此筛选本次启动产生的 crash-report）
            var launchStartedAt = DateTime.Now;

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
            if (!launchToken.IsCancellationRequested)
                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "launch-progress", stage = "parse" }));
            var parser = new MinecraftParser(minecraftPath);
            var game = parser.GetMinecraft(gameId);
            if (game == null)
            {
                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = L("未找到选中的游戏版本", "Selected game version not found") }));
                return;
            }
            if (launchToken.IsCancellationRequested) { CleanupCancelled(window, null, generation); return; }

            // 3. 查找 Java 运行时（后台线程执行，避免阻塞 UI）
            if (!launchToken.IsCancellationRequested)
                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "launch-progress", stage = "java" }));
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

            // Java 版本校验：手动指定 Java 时按目标 MC 版本所需 Java 大版本给出警告（不阻断启动；
            // 自动路径已由库 GetAppropriateJava 按相同规则匹配）
            if (javaPath is not ("__auto__" or ""))
            {
                var requiredJava = game.GetAppropriateJavaVersion();
                if (java.MajorVersion < requiredJava)
                    TryNotifyWindow(window, JsonSerializer.Serialize(new
                    {
                        type = "launch-warning",
                        message = L(
                            $"警告：当前 Java {java.MajorVersion} 低于该游戏版本所需的 Java {requiredJava}，可能出现兼容性问题",
                            $"Warning: Java {java.MajorVersion} is lower than the required Java {requiredJava} for this game version. Compatibility issues may occur.")
                    }));
            }

            // Windows 下使用 javaw.exe 避免启动时弹出控制台黑框。
            // JavaEntry.JavaPath 是 init-only 属性，反射赋值收敛于 MinecraftLaunchFallbacks
            if (OperatingSystem.IsWindows())
                MinecraftLaunchFallbacks.OverrideJavaPath(java, java.JavaPath.Replace("java.exe", "javaw.exe"));

            // 版本隔离：版本目录下存在 mods 或 saves 时启用独立游戏目录（IsEnableIndependency），
            // 游戏数据落在 versions/<id>/ 下，防止不同版本共享数据冲突；
            // 否则共享 .minecraft 根目录（与官方启动器行为一致）
            var versionDir = Path.Combine(minecraftPath, "versions", gameId);
            var isolated = Directory.Exists(Path.Combine(versionDir, "mods"))
                || Directory.Exists(Path.Combine(versionDir, "saves"));

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
                IsEnableIndependency = isolated,
            };

            // 5. 启动游戏
            var runner = new MinecraftRunner(config, parser);
            Log.Debug("[Launch] 正在调用 RunAsync...");
            // 阶段进度：进入启动（RunAsync 内含原生库解压、参数构建、进程启动）
            if (!launchToken.IsCancellationRequested)
                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "launch-progress", stage = "run" }));
            try
            {
                RunningProcess = await runner.RunAsync(gameId, launchToken);
            }
            catch (OperationCanceledException)
            {
                Log.Debug("[Launch] 启动已取消");
                CleanupCancelled(window, null, generation);
                return;
            }
            catch (Exception rex)
            {
                Log.Debug($"[Launch] RunAsync 异常: {rex}");
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

            // 阶段进度：进程已创建，等待游戏窗口出现（轮询任务发送 game-launched）
            if (!launchToken.IsCancellationRequested)
                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "launch-progress", stage = "waiting" }));

            Log.Debug($"[Launch] ArgumentList 数量: {RunningProcess.ArgumentList.Count()}");
            foreach (var a in RunningProcess.ArgumentList.Take(5))
                Log.Debug($"[Launch] Arg: {a}");

            // 空参数列表：Fabric/Quilt 等模组版本在 MinecraftRunner 内部 Parse 失败
            // 直接用 ArgumentsParser 独立生成参数，绕过内部可能的异常吞没
            if (RunningProcess.Process == null)
            {
                Log.Debug("[Launch] 参数列表为空，独立构造 ArgumentsParser");
                try
                {
                    var argParser = new ArgumentsParser(game, config);
                    var arguments = argParser.Parse().ToList();
                    Log.Debug($"[Launch] 独立 Parse 结果: {arguments.Count} 项");
                    foreach (var a in arguments.Take(3))
                        Log.Debug($"[Launch] Arg: {a}");

                    if (arguments.Count == 0)
                        throw new InvalidOperationException("ArgumentsParser 返回空参数列表");

                    // 确保 native 库已解压（RunAsync 内可能因异常提前返回未执行解压；幂等）
                    ExtractNativesFallback(game, minecraftPath);

                    var proc = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo(java.JavaPath)
                        {
                            // 逐参数加引号，避免含空格路径（classpath/natives 等）被拆成多个参数
                            Arguments = CommandLineBuilder.JoinArguments(arguments),
                            WorkingDirectory = Path.Combine(minecraftPath, "versions", gameId),
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        },
                        EnableRaisingEvents = true
                    };
                    // MinecraftProcess.Process 是 init-only 属性，反射注入收敛于 MinecraftLaunchFallbacks
                    MinecraftLaunchFallbacks.AttachProcess(RunningProcess, proc);
                    if (launchToken.IsCancellationRequested) { CleanupCancelled(window, RunningProcess, generation); return; }
                    RunningProcess.Start();
                }
                catch (Exception ex)
                {
                    Log.Debug($"[Launch] ArgumentsParser 失败: {ex.Message}");
                    // Fallback：手动构造启动参数，绕过 MinecraftLaunch 的 ParseJsonNode bug
                    try
                    {
                        var fallbackArgs = ArgumentTemplateEngine.BuildFallbackArgs(game, config, java, minecraftPath, isolated);
                        Log.Debug($"[Launch] Fallback 参数: {fallbackArgs.Count} 项");

                        // 确保 native 库已解压到 versions/<id>/natives（fallback 的 natives_directory 指向该处）
                        ExtractNativesFallback(game, minecraftPath);

                        var proc = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo(java.JavaPath)
                            {
                                // 逐参数加引号，避免含空格路径（classpath/natives 等）被拆成多个参数
                                Arguments = CommandLineBuilder.JoinArguments(fallbackArgs),
                                WorkingDirectory = Path.Combine(minecraftPath, "versions", gameId),
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            },
                            EnableRaisingEvents = true
                        };
                        // MinecraftProcess.Process 是 init-only 属性，反射注入收敛于 MinecraftLaunchFallbacks
                        MinecraftLaunchFallbacks.AttachProcess(RunningProcess, proc);

                        // 手动绑定控制台输出（MinecraftProcess 内部事件在构造函数提前 return 后未绑定）
                        proc.OutputDataReceived += (_, e) => {
                            if (!string.IsNullOrEmpty(e.Data))
                                Log.Debug($"[MC] {e.Data}");
                        };
                        proc.ErrorDataReceived += (_, e) => {
                            if (!string.IsNullOrEmpty(e.Data))
                                Log.Debug($"[MC] ERR: {e.Data}");
                        };

                        // 手动绑定退出事件（MinecraftProcess 内置回调在构造器提前 return 后未绑定）
                        var mpRef = RunningProcess;
                        proc.Exited += (_, _) =>
                        {
                            Log.Debug("[MC] 游戏进程已退出（fallback 路径）");
                            // 崩溃分析：异常退出（退出码非 0）时解析 crash-reports
                            if (proc.ExitCode != 0)
                                TryAnalyzeCrash(window, game, minecraftPath, launchStartedAt);
                            proc.Dispose();
                            // 与正常路径一致：仅当仍是被跟踪的进程时才清理引用并通知前端，
                            // 防止旧进程迟到的退出事件清掉下一次启动的进程引用
                            if (ReferenceEquals(RunningProcess, mpRef))
                            {
                                RunningProcess = null;
                                TryNotifyWindow(window, GameMessages.GameExited);
                            }
                        };

                        if (launchToken.IsCancellationRequested) { CleanupCancelled(window, RunningProcess, generation); return; }
                        // MinecraftProcess.Start() 内部已执行 Process.Start() + BeginOutputReadLine/BeginErrorReadLine，
                        // 此处不得重复调用（否则抛 async read already started）
                        RunningProcess.Start();
                    }
                    catch (Exception fallbackEx)
                    {
                        Log.Debug($"[Launch] 手动启动失败: {fallbackEx}");
                        TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = $"{L("启动失败", "Launch failed")}: {fallbackEx.Message}" }));
                        return;
                    }
                }
            }

            Log.Debug("[Launch] RunAsync 完成");

            // 注册日志和退出事件
            RunningProcess.OutputLogReceived += (_, arg) =>
                Log.Debug($"[MC] {arg.Data.Log}");

            var processRef = RunningProcess;
            RunningProcess.Exited += (_, _) =>
            {
                Log.Debug($"[MC] 游戏进程已退出，退出码: {processRef.Process?.ExitCode}");
                // 崩溃分析：异常退出（退出码非 0）时解析 crash-reports
                if (processRef.Process?.ExitCode != 0)
                    TryAnalyzeCrash(window, game, minecraftPath, launchStartedAt);
                processRef?.Dispose();
                // 仅当仍是被跟踪的进程时才清理引用并通知前端：
                // 用户关闭游戏时 CloseGame 已清空引用并发送 game-exited，
                // 迟到的退出事件不得误伤下一次启动的进程引用或重复复位
                if (RunningProcess == processRef)
                {
                    RunningProcess = null;
                    TryNotifyWindow(window, GameMessages.GameExited);
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
                            Log.Debug("[Launch] 轮询终止: 启动已被替代或 RunningProcess 已被清空");
                            return;
                        }
                        if (processRef.Process?.HasExited == true)
                        {
                            // 退出事件处理器可能已接管（RunningProcess 已被清空），避免与 game-exited 重复报错
                            if (RunningProcess != processRef)
                            {
                                Log.Debug("[Launch] 轮询终止: 退出事件已处理");
                                return;
                            }
                            Log.Debug($"[MC] 启动失败，退出码: {processRef.Process.ExitCode}");
                            // 崩溃分析：有分析结果时用中文解释替代通用报错
                            if (!TryAnalyzeCrash(window, game, minecraftPath, launchStartedAt))
                                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = L("游戏启动失败，请检查版本完整性", "Game failed to start. Please check version integrity.") }));
                            processRef.Dispose();
                            if (RunningProcess == processRef) RunningProcess = null;
                            // 前端复位（crash-analysis 已作为通知显示）
                            TryNotifyWindow(window, GameMessages.GameExited);
                            return;
                        }
                        try
                        {
                            if (processRef.Process?.MainWindowHandle != IntPtr.Zero)
                            {
                                if (!IsCurrent())
                                {
                                    Log.Debug("[Launch] 轮询终止: 窗口出现前启动已被替代");
                                    return;
                                }
                                Log.Debug("[Launch] 游戏窗口已出现");
                                TryNotifyWindow(window, GameMessages.GameLaunched);
                                return;
                            }
                        }
                        // MainWindowHandle 访问在窗口关闭竞态下可能抛 InvalidOperationException，
                        // 轮询期间静默忽略（下一轮循环会经由 HasExited 分支处理退出）
                        catch { }
                        await Task.Delay(500);
                    }
                    // 超时视为已启动，但发送前需确认本次启动仍是最新且进程引用未被清空，
                    // 防止与退出事件竞态导致 game-launched 晚于 game-exited 到达前端（UI 永久卡在「运行中」）
                    if (!IsCurrent() || RunningProcess != processRef)
                    {
                        Log.Debug("[Launch] 窗口检测超时，但启动已被替代，不发送 game-launched");
                        return;
                    }
                    Log.Debug("[Launch] 窗口检测超时，视为已启动");
                    TryNotifyWindow(window, GameMessages.GameLaunched);
                }
                catch (Exception ex)
                {
                    // 代次守卫：启动已被替代时，迟到的轮询异常不得误报给前端
                    if (!IsCurrent())
                    {
                        Log.Debug("[Launch] 轮询异常但启动已被替代，忽略");
                        return;
                    }
                    Log.Debug($"[Launch] 状态检查异常: {ex.Message}");
                    TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "game-error", message = L("游戏启动失败，请检查版本完整性", "Game failed to start. Please check version integrity.") }));
                }
            });
        }
        catch (OperationCanceledException)
        {
            // 取消（如 Java 枚举的 await 直接抛 OCE）不是启动失败，走取消清理路径而非报 game-error
            Log.Debug("[Launch] 启动已取消（外层）");
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
            Log.Debug($"[Launch] 取消清理失败: {ex.Message}");
        }
        if (generation == Volatile.Read(ref LaunchGeneration))
            TryNotifyWindow(window, GameMessages.GameExited);
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
            Log.Debug($"[Window] 消息发送失败（窗口可能已关闭）: {ex.Message}");
        }
    }
}
