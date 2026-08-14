// native 库解压 fallback（从 Program.Launch.cs 拆分出的职责，逐字移动未改逻辑）
// native 库解压（fallback 路径手动解压 natives）
// 游戏数据模型（MinecraftEntry / ModifiedMinecraftEntry）
using MinecraftLaunch.Base.Models.Game;
using System.IO.Compression;
// JSON 解析（AOT 安全的 JsonDocument）
using System.Text.Json;

namespace DeciLauncher;

partial class Program
{
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
            Log.Debug($"[WARN] ExtractNativesFallback 失败: {ex.Message}");
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
                    Log.Debug($"[Launch] 解压 native: {target}");
                }
            }
            return true;
        }
    }
}
