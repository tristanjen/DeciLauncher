// Java 运行时自动检测
using MinecraftLaunch.Utilities;
// JavaEntry（Java 运行时信息）
using MinecraftLaunch.Base.Models.Game;
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

            var javas = await EnumerateJavaEntriesAsync();
            var items = javas
                .Select(java => (object)new { path = java.JavaPath, version = java.JavaVersion })
                .ToList();

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

    /// <summary>
    /// 枚举可用 Java 运行时（设置页扫描与游戏启动共用）。
    /// Windows 沿用 MinecraftLaunch 的注册表 + `where javaw` 扫描；macOS/Linux 下库的
    /// whereis 扫描恒为空（原因见下方注释），改为自行枚举常见位置后用库解析版本。
    /// 结果按解析符号链接后的真实路径去重，跳过无法解析版本的条目。
    /// 返回空列表表示未检测到可用 Java，由调用方决定提示方式。
    /// </summary>
    private static async Task<List<JavaEntry>> EnumerateJavaEntriesAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<JavaEntry>();
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);

        void Add(JavaEntry? java)
        {
            if (java == null || string.IsNullOrEmpty(java.JavaVersion) || string.IsNullOrEmpty(java.JavaPath))
                return;

            string key;
            try { key = File.ResolveLinkTarget(java.JavaPath, true)?.FullName ?? Path.GetFullPath(java.JavaPath); }
            catch { key = Path.GetFullPath(java.JavaPath); }

            if (seenPaths.Add(key)) items.Add(java);
        }

        if (OperatingSystem.IsWindows())
        {
            await foreach (var java in JavaUtil.EnumerableJavaAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                Add(java);
            }
        }
        else
        {
            // MinecraftLaunch 4.0.7 的 Unix 扫描依赖 `whereis -b java` 的输出来判断路径，
            // 但 whereis 实际输出 "java: /usr/bin/java"（整行不是路径），File.Exists 必然失败，
            // 导致 macOS/Linux 扫描恒为空（甚至可能 yield 出 null 条目）；
            // 且 macOS 的 /usr/bin/java 只是系统存根。
            // 这里自行枚举常见位置，再复用库的 GetJavaInfoAsync 解析版本。
            foreach (var candidate in EnumerateJavaCandidates())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    Add(await JavaUtil.GetJavaInfoAsync(candidate, cancellationToken));
                }
                catch (OperationCanceledException)
                {
                    // 取消不是「候选不可用」，必须向上传播（外层按取消路径清理）
                    throw;
                }
                catch (Exception ex)
                {
                    // 不可用的候选（如未安装 JVM 时的 /usr/bin/java 存根）跳过即可
                    Log.Warn($"[Java] 无法解析 {candidate}: {ex.Message}");
                }
            }
        }

        return items;
    }

    /// <summary>
    /// 枚举非 Windows 平台可能安装 Java 的位置：
    /// JAVA_HOME、PATH、系统 JVM 目录（macOS 的 JavaVirtualMachines / Linux 的 /usr/lib/jvm 等）。
    /// 仅返回真实存在的文件，版本解析交给 JavaUtil.GetJavaInfoAsync。
    /// </summary>
    private static IEnumerable<string> EnumerateJavaCandidates()
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        void Add(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            string key;
            try { key = File.ResolveLinkTarget(path, true)?.FullName ?? Path.GetFullPath(path); }
            catch { key = Path.GetFullPath(path); }
            if (seen.Add(key)) candidates.Add(path);
        }

        void AddJavaHomes(string root, string relativeJavaPath)
        {
            try
            {
                if (!Directory.Exists(root)) return;
                foreach (var home in Directory.EnumerateDirectories(root))
                    Add(Path.Combine(home, relativeJavaPath));
            }
            catch (Exception ex)
            {
                // 权限不足等：忽略该目录，继续其它候选
                Log.Warn($"[Java] 无法枚举 {root}: {ex.Message}");
            }
        }

        // 1) JAVA_HOME（优先真实路径：macOS 的 /usr/bin/java 只是系统存根）
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
            Add(Path.Combine(javaHome, "bin", "java"));

        // 2) 平台标准安装目录
        if (OperatingSystem.IsMacOS())
        {
            // macOS JDK 的布局为 <JVM>.jdk/Contents/Home/bin/java
            AddJavaHomes("/Library/Java/JavaVirtualMachines", Path.Combine("Contents", "Home", "bin", "java"));
            AddJavaHomes(Path.Combine(userHome, "Library", "Java", "JavaVirtualMachines"),
                Path.Combine("Contents", "Home", "bin", "java"));
        }
        else if (OperatingSystem.IsLinux())
        {
            AddJavaHomes("/usr/lib/jvm", Path.Combine("bin", "java"));
            AddJavaHomes("/usr/java", Path.Combine("bin", "java"));
            AddJavaHomes("/opt/java", Path.Combine("bin", "java"));
            AddJavaHomes(Path.Combine(userHome, ".sdkman", "candidates", "java"), Path.Combine("bin", "java"));
        }

        // 3) PATH 中的 java（Homebrew / SDKMAN / 手动解压的 JDK 通常只在这里）。
        // macOS 的 /usr/bin/java 是系统存根：已找到真实 JDK 时跳过，避免同一版本重复展示
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, "java");
            if (OperatingSystem.IsMacOS() && candidate == "/usr/bin/java" && candidates.Count > 0)
                continue;
            Add(candidate);
        }

        return candidates;
    }
}
