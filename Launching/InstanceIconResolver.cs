// 图标解析纯函数：根据实例类型返回「内置图标标识」，前端据此渲染打包的美工图标。
// 采用与 HMCL/PCL 一致的做法——启动器内置成品图标（草方块/命令方块/铁砧/加载器 logo），
// 而非运行时从游戏 assets 抽取原始平面纹理（原始纹理是给 3D 立方体贴图用的，
// 直接当图标会缺乏立体感/背景，观感不正确）。
//
// 判定规则（与用户确认）：
//   - 原版（无模组加载器，非快照）：grass（草方块图标）
//   - 快照/预发布（含 OldBeta/OldAlpha、新式 -snapshot-N 命名）：command（命令方块图标）
//   - Forge：Forge（铁砧图标，前端素材 forge.webp）
//   - Fabric / Quilt / NeoForge：对应加载器名（Fabric/Quilt/NeoForge），前端渲染各自内置 logo
//   - 其余加载器（Cauldron/LiteLoader/OptiFine/Unknown）：command（回退命令方块）

using MinecraftLaunch.Base.Enums;
using MinecraftLaunch.Base.Models.Game;

namespace DeciLauncher;

internal static class InstanceIconResolver
{
    /// <summary>
    /// 前端有内置 logo 的加载器类型（ModLoaderType 名）。
    /// Forge 不在其列——Forge 返回 "Forge"，前端用铁砧素材 forge.webp 渲染。
    /// </summary>
    private static readonly HashSet<ModLoaderType> LogoLoaders =
    [
        ModLoaderType.Fabric, ModLoaderType.Quilt, ModLoaderType.NeoForge
    ];

    /// <summary>
    /// 解析单个实例的内置图标标识。
    /// </summary>
    /// <param name="game">MinecraftLaunch 解析出的实例条目（含 Version.Type 与修改版 ModLoaders）。</param>
    /// <returns>图标标识：grass / command / Forge / Fabric / Quilt / NeoForge。</returns>
    internal static string Resolve(MinecraftEntry game)
    {
        // 1. 模组实例：Fabric/Quilt/NeoForge 走内置 logo；Forge 走铁砧图标
        if (game is ModifiedMinecraftEntry { ModLoaders: { } loaders })
        {
            foreach (var loader in loaders)
            {
                if (LogoLoaders.Contains(loader.Type))
                    return loader.Type.ToString();

                if (loader.Type == ModLoaderType.Forge)
                    return "Forge";
            }

            // 带加载器但非上述类型：回退命令方块
            return "command";
        }

        // 2. 原版：快照用命令方块，正式版用草方块
        return IsSnapshotVersion(game) ? "command" : "grass";
    }

    /// <summary>
    /// 判断实例是否为「快照 / 预发布」类版本。
    /// 优先用 Version.Type；当库因新式命名（如 26.3-snapshot-8）误判为 Release 时，
    /// 用版本 Id 字符串关键词兜底。
    /// </summary>
    private static bool IsSnapshotVersion(MinecraftEntry game)
    {
        // 库已正确识别的快照/旧版类型（旧式命名）
        if (game.Version.Type is MinecraftVersionType.Snapshot
            or MinecraftVersionType.OldBeta
            or MinecraftVersionType.OldAlpha)
            return true;

        // 字符串兜底：新式快照/预发布命名里含这些片段
        var id = game.Id;
        if (string.IsNullOrEmpty(id))
            return false;

        return id.Contains("-snapshot", StringComparison.OrdinalIgnoreCase)
            || id.Contains("-pre", StringComparison.OrdinalIgnoreCase)
            || id.Contains("-rc", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("beta", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("alpha", StringComparison.OrdinalIgnoreCase);
    }
}
