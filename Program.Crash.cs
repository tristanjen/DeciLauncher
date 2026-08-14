// 崩溃分析（todolist #7）：游戏异常退出后解析 crash-reports，
// 基于 MinecraftLaunch LogAnalyzer 识别崩溃原因并给出中文解释。

// JSON 序列化（崩溃分析消息回传前端）
// 崩溃原因枚举 + 分析结果模型
using MinecraftLaunch.Base.Enums;
// 游戏数据模型
using MinecraftLaunch.Base.Models.Game;
// 日志分析器（崩溃原因识别 + 可疑 Mod 提取）
using MinecraftLaunch.Components.Logging;
// Photino 窗口（前端消息回传）
using Photino.NET;
using System.Text.Json;

namespace DeciLauncher;

partial class Program
{
    /// <summary>
    /// 游戏异常退出后解析最新 crash-report 并向前端发送中文解释。
    /// 返回是否已发送 crash-analysis 消息（无崩溃报告或分析无结果时返回 false）。
    /// </summary>
    private static bool TryAnalyzeCrash(PhotinoWindow window, MinecraftEntry game, string minecraftPath, DateTime launchStartedAt)
    {
        try
        {
            var crashDir = Path.Combine(minecraftPath, "crash-reports");
            if (!Directory.Exists(crashDir)) return false;

            // 仅分析本次启动产生的崩溃报告（按修改时间过滤，避免旧报告误报）
            var latest = Directory.GetFiles(crashDir, "crash-*.txt")
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTime >= launchStartedAt)
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();
            if (latest == null) return false;

            // 拒绝符号链接/reparse point（避免读取任意位置文件并把片段回显到 UI）
            if (latest.LinkTarget != null) return false;

            var analyzer = new LogAnalyzer(game, new[] { latest.FullName });
            var result = analyzer.Analyze();
            if (result.CrashReasons.Count == 0 && result.SuspiciousMods.Count == 0) return false;

            var lines = result.CrashReasons
                .Select(r => CrashReasonMapper.Map(r, L))
                .Where(r => r != null)
                .Distinct()
                .ToList();
            if (result.SuspiciousMods.Count > 0)
                lines.Add(L(
                    $"可疑 Mod：{string.Join("、", result.SuspiciousMods)}",
                    $"Suspicious mods: {string.Join(", ", result.SuspiciousMods)}"));
            lines.Add(L(
                $"崩溃报告：{Path.GetFileName(latest.FullName)}",
                $"Crash report: {Path.GetFileName(latest.FullName)}"));

            TryNotifyWindow(window, JsonSerializer.Serialize(new
            {
                type = "crash-analysis",
                message = string.Join("\n", lines)
            }));
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug($"[WARN] 崩溃分析失败: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// CrashReasons → 用户可读的双语解释映射（独立类以便单元测试）；
/// 未知/无意义的原因返回 null（不显示）。
/// </summary>
internal static class CrashReasonMapper
{
    /// <summary>
    /// 按原因返回双语解释；l 为语言选择器（生产环境传 Program.L，测试传固定语言）
    /// </summary>
    internal static string? Map(CrashReasons reason, Func<string, string, string> l) => reason switch
    {
        CrashReasons.InsufficientMemory => l(
            "内存不足：游戏因内存不足而崩溃，请尝试在设置中提高内存分配，或关闭其他占用内存的程序",
            "Insufficient memory: the game crashed due to low memory. Try raising the memory allocation in settings or closing other memory-heavy programs."),
        CrashReasons.GraphicsCardDoesNotSupportOpenGL => l(
            "显卡或驱动不支持 OpenGL：请更新显卡驱动，或检查显卡是否满足游戏要求",
            "Graphics card or driver does not support OpenGL: update your graphics driver or check whether your GPU meets the game's requirements."),
        CrashReasons.ModCausedGameCrash or CrashReasons.ModLoaderError
            or CrashReasons.ModInitializationFailed or CrashReasons.ModMixinFailed => l(
            "Mod 冲突或损坏导致崩溃：请检查最近安装/更新的 Mod，尝试逐个移除定位问题",
            "A mod conflict or corrupted mod caused the crash: check recently installed/updated mods and try removing them one by one."),
        CrashReasons.ModInstalledRepeatedly => l(
            "存在重复安装的 Mod：请检查 mods 目录并移除重复的 Mod 文件",
            "Duplicate mods detected: check the mods folder and remove duplicated mod files."),
        CrashReasons.TooManyModsExceededIdLimit => l(
            "Mod 数量超出游戏 ID 上限：请移除部分 Mod",
            "Too many mods (exceeded the ID limit): remove some mods."),
        CrashReasons.ModFileDecompressed => l(
            "存在被解压的 Mod 文件：解压后的 Mod jar 无法加载，请重新下载原始 jar 文件",
            "Extracted mod jars found: extracted mod jars cannot be loaded. Re-download the original jar files."),
        CrashReasons.ModConfigCausedGameCrash => l(
            "Mod 配置错误导致崩溃：请检查对应 Mod 的配置文件",
            "A mod config error caused the crash: check the configuration file of the related mod."),
        CrashReasons.JavaVersionTooHigh => l(
            "Java 版本过高：该游戏版本不支持当前 Java，请换用更低版本的 Java",
            "Java version too high: this game version does not support the current Java. Use an older Java."),
        CrashReasons.UnsupportedJavaClassVersionError => l(
            "Java 版本与游戏不匹配（class version 错误）：请按游戏版本要求选择 Java（1.17+ 需 Java 17，1.20.5+ 需 Java 21）",
            "Java version mismatch (class version error): pick a Java that matches the game version (Java 17 for 1.17+, Java 21 for 1.20.5+)."),
        CrashReasons.LowVersionForgeIncompatibleWithHighVersionJava => l(
            "Forge 版本过低，与当前 Java 不兼容：请升级 Forge 或换用更低版本的 Java",
            "Forge version too old for the current Java: upgrade Forge or use an older Java."),
        CrashReasons.UsingJDK => l(
            "正在使用 JDK 而非 JRE：建议换用标准 JRE 运行游戏",
            "A JDK is being used instead of a JRE: prefer a standard JRE to run the game."),
        CrashReasons.UsingOpenJ9 => l(
            "不支持的 OpenJ9 虚拟机：请换用 HotSpot 虚拟机（如 Adoptium/Temurin）",
            "Unsupported OpenJ9 VM: switch to a HotSpot-based Java (e.g. Adoptium/Temurin)."),
        CrashReasons.Using32BitJavaCausedInsufficientJVMMemory => l(
            "32 位 Java 内存受限导致崩溃：请换用 64 位 Java",
            "32-bit Java has limited memory: switch to a 64-bit Java."),
        CrashReasons.OptiFineIncompatibleWithForge => l(
            "OptiFine 与当前 Forge 版本不兼容：请更换 OptiFine 或 Forge 版本",
            "OptiFine is incompatible with the current Forge version: change the OptiFine or Forge version."),
        CrashReasons.OptiFineCausedWorldLoadingFailure => l(
            "OptiFine 导致世界加载失败：请移除或更换 OptiFine 版本",
            "OptiFine caused a world loading failure: remove or change the OptiFine version."),
        CrashReasons.MultipleForgeInVersionJson => l(
            "版本 JSON 中存在多个 Forge 版本：请删除该版本并重新安装 Forge",
            "Multiple Forge versions found in the version JSON: delete the version and reinstall Forge."),
        CrashReasons.PlayerTriggeredDebugCrash => l(
            "检测到人为触发的调试崩溃（F3+C）：这是正常行为，无需处理",
            "A manually triggered debug crash (F3+C) was detected: this is expected behavior."),
        CrashReasons.ShaderOrResourcePackCausedOpenGL1282Error => l(
            "光影或资源包引发 OpenGL 错误：请更换光影/资源包或更新显卡驱动",
            "A shader or resource pack caused an OpenGL error: change the shader/resource pack or update your graphics driver."),
        CrashReasons.TextureTooLargeOrInsufficientGraphicsConfig => l(
            "纹理过大或显卡配置不足：请尝试降低资源包分辨率或画质设置",
            "Texture too large or insufficient graphics config: try a lower-resolution resource pack or lower graphics settings."),
        CrashReasons.FileOrContentCheckFailed => l(
            "游戏文件校验失败：文件可能损坏，请删除该版本重新安装",
            "Game file verification failed: files may be corrupted. Delete the version and reinstall it."),
        CrashReasons.ForgeError => l(
            "Forge 发生错误：请查看崩溃报告获取详细信息",
            "Forge reported an error: see the crash report for details."),
        CrashReasons.FabricError or CrashReasons.FabricErrorWithSolution => l(
            "Fabric 发生错误：请查看崩溃报告获取详细信息",
            "Fabric reported an error: see the crash report for details."),
        CrashReasons.SpecificBlockCausedCrash or CrashReasons.SpecificEntityCausedCrash => l(
            "特定方块/实体导致崩溃：请尝试移除相关世界数据或 Mod",
            "A specific block/entity caused the crash: try removing the related world data or mod."),
        CrashReasons.UnableToLoadTexture => l(
            "纹理加载失败：资源包可能损坏，请更换资源包",
            "Failed to load a texture: the resource pack may be corrupted. Change the resource pack."),
        _ => null,
    };
}
