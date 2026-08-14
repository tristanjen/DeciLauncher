// 启动参数模板引擎与 classpath 构建（从 Program.Launch.cs 抽取的纯函数，逻辑未变）
// 背景：MinecraftLaunch 4.0.7 的 ArgumentsParser 存在 ParseJsonNode bug，
// 这些函数是绕过库内部异常吞没的 fallback 路径，需要独立测试覆盖。

using System.Text.Json;
using MinecraftLaunch.Base.Models.Game;
using MinecraftLaunch.Launch;

namespace DeciLauncher;

/// <summary>
/// 从 version.json 手工构建启动参数（绕过 MinecraftLaunch ArgumentsParser 的 ParseJsonNode bug）。
/// 原为 Program 的私有静态方法，抽取为独立静态类以便单元测试。
/// </summary>
internal static class ArgumentTemplateEngine
{
    /// <summary>
    /// 手动构造启动参数（绕过ArgumentsParser的ParseJsonNode bug）
    /// </summary>
    internal static List<string> BuildFallbackArgs(
        MinecraftEntry game, LaunchConfig config,
        JavaEntry java, string minecraftPath, bool isolated)
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
        var classPath = LibraryPathMapper.BuildClassPath(game, minecraftPath);
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
            // 版本隔离：isolated 时游戏数据落在 versions/<id>/，否则共享 .minecraft 根目录
            {"${game_directory}", isolated ? Path.Combine(minecraftPath, "versions", game.Id) : minecraftPath},
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

    internal static void ReadJvmArgs(string jsonContent, List<string> args)
    {
        try
        {
            using var json = JsonDocument.Parse(jsonContent);
            var root = json.RootElement;
            if (root.TryGetProperty("arguments", out var argsObj) && argsObj.TryGetProperty("jvm", out var jvmArr))
                AppendVersionArgs(args, jvmArr);
        }
        catch (Exception ex) { Log.Debug($"[WARN] ReadJvmArgs: {ex.Message}"); }
    }

    internal static void ReadGameArgs(string jsonContent, List<string> args)
    {
        try
        {
            using var json = JsonDocument.Parse(jsonContent);
            var root = json.RootElement;
            if (root.TryGetProperty("arguments", out var argsObj) && argsObj.TryGetProperty("game", out var gameArr))
                AppendVersionArgs(args, gameArr);
            else if (root.TryGetProperty("minecraftArguments", out var mcArgs))
                // 旧版单行参数：引号感知拆分（含引号的 token 不按空格拆）
                args.AddRange(CommandLineBuilder.SplitArgsRespectingQuotes(mcArgs.GetString() ?? ""));
        }
        catch (Exception ex) { Log.Debug($"[WARN] ReadGameArgs: {ex.Message}"); }
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

    /// <summary>
    /// 判定版本 JSON 中参数规则（rules）是否允许在当前系统上生效
    /// </summary>
    internal static bool RulesAllow(JsonElement rules)
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
}

/// <summary>
/// 从版本 JSON 解析 Maven 库路径并构建 classpath（绕过 MinecraftLaunch 的 ParseJsonNode bug）。
/// 原为 Program 的私有静态方法，抽取为独立静态类以便单元测试。
/// </summary>
internal static class LibraryPathMapper
{
    /// <summary>
    /// 构建 JVM classpath：vanilla client jar + 核心 jar + 全部库文件（去重）
    /// </summary>
    internal static string BuildClassPath(MinecraftEntry game, string minecraftPath)
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
    internal static List<string> ParseLibraryPaths(MinecraftEntry game, string minecraftPath)
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
            Log.Debug($"[WARN] ReadLibraryPaths: {ex.Message}");
        }
    }

    /// <summary>
    /// 将 Maven 库名转换为文件路径。
    /// 支持带 classifier 的四段名（如 org.lwjgl:lwjgl:3.2.1:natives-windows）
    /// → &lt;artifact&gt;-&lt;version&gt;-&lt;classifier&gt;.jar
    /// </summary>
    internal static string LibraryNameToPath(string name)
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
}
