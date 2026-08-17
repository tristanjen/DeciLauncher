// InstanceIconResolver 单元测试：锁定「实例 → 内置图标标识」的判定。
// 原版正式版 → grass；快照/预发布 → command；Forge → Forge（铁砧图标，素材 forge.webp）；
// Fabric/Quilt/NeoForge → 各自加载器名；其余加载器 → command。

using DeciLauncher;
using MinecraftLaunch.Base.Enums;
using MinecraftLaunch.Base.Models.Game;

namespace DeciLauncher.Tests;

public class InstanceIconResolverTests
{
    private VanillaMinecraftEntry Vanilla(MinecraftVersionType type, string id = "1.0")
    {
        return new VanillaMinecraftEntry
        {
            Id = id,
            Version = new MinecraftVersion(id, type),
            ClientJarPath = "unused.jar",
            ClientJsonPath = "unused.json",
            AssetIndexJsonPath = "unused.json",
            MinecraftFolderPath = "unused",
            ReleaseTime = DateTime.UnixEpoch,
        };
    }

    private ModifiedMinecraftEntry Modified(params ModLoaderType[] loaders)
    {
        return new ModifiedMinecraftEntry
        {
            Id = "1.0",
            Version = new MinecraftVersion("1.0", MinecraftVersionType.Release),
            ClientJarPath = "unused.jar",
            ClientJsonPath = "unused.json",
            AssetIndexJsonPath = "unused.json",
            MinecraftFolderPath = "unused",
            ReleaseTime = DateTime.UnixEpoch,
            ModLoaders = loaders.Select(t => new ModLoaderInfo(t, "1.0")).ToArray(),
        };
    }

    [Fact]
    public void VanillaRelease_ReturnsGrass()
    {
        Assert.Equal("grass", InstanceIconResolver.Resolve(Vanilla(MinecraftVersionType.Release)));
    }

    [Fact]
    public void VanillaSnapshot_ReturnsCommand()
    {
        Assert.Equal("command", InstanceIconResolver.Resolve(Vanilla(MinecraftVersionType.Snapshot)));
    }

    [Fact]
    public void VanillaOldBeta_ReturnsCommand()
    {
        Assert.Equal("command", InstanceIconResolver.Resolve(Vanilla(MinecraftVersionType.OldBeta)));
    }

    [Fact]
    public void VanillaOldAlpha_ReturnsCommand()
    {
        Assert.Equal("command", InstanceIconResolver.Resolve(Vanilla(MinecraftVersionType.OldAlpha)));
    }

    [Fact]
    public void ModernSnapshotId_ReturnsCommand_EvenWhenLibSaysRelease()
    {
        // Minecraft 26 新式快照命名（26.3-snapshot-8）会被库误判为 Release，
        // 但 IsSnapshotVersion 的字符串兜底应仍识别为快照。
        Assert.Equal("command", InstanceIconResolver.Resolve(Vanilla(MinecraftVersionType.Release, "26.3-snapshot-8")));
    }

    [Fact]
    public void ModernReleaseId_ReturnsGrass()
    {
        Assert.Equal("grass", InstanceIconResolver.Resolve(Vanilla(MinecraftVersionType.Release, "26.2")));
    }

    [Fact]
    public void Forge_ReturnsForge()
    {
        Assert.Equal("Forge", InstanceIconResolver.Resolve(Modified(ModLoaderType.Forge)));
    }

    [Fact]
    public void Fabric_ReturnsFabric()
    {
        Assert.Equal("Fabric", InstanceIconResolver.Resolve(Modified(ModLoaderType.Fabric)));
    }

    [Fact]
    public void Quilt_ReturnsQuilt()
    {
        Assert.Equal("Quilt", InstanceIconResolver.Resolve(Modified(ModLoaderType.Quilt)));
    }

    [Fact]
    public void NeoForge_ReturnsNeoForge()
    {
        Assert.Equal("NeoForge", InstanceIconResolver.Resolve(Modified(ModLoaderType.NeoForge)));
    }

    [Fact]
    public void UnknownLoader_ReturnsCommand()
    {
        Assert.Equal("command", InstanceIconResolver.Resolve(Modified(ModLoaderType.OptiFine)));
        Assert.Equal("command", InstanceIconResolver.Resolve(Modified(ModLoaderType.Cauldron)));
        Assert.Equal("command", InstanceIconResolver.Resolve(Modified(ModLoaderType.LiteLoader)));
        Assert.Equal("command", InstanceIconResolver.Resolve(Modified(ModLoaderType.Unknown)));
    }
}
