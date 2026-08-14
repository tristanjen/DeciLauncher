// BMCLAPI 镜像源行为锁定测试（todolist #1）：
// 库内置 BmclApiSource 在 DownloadManager.IsEnableMirror=true 时把 Mojang/Forge/Fabric
// 等下载 URL 重写为 bmclapi2.bangbang93.com 国内 CDN。
// 开关由用户下载源偏好驱动（默认 official-first，等价官方源），见 stores/downloadSource.ts。

using MinecraftLaunch;

namespace DeciLauncher.Tests;

public class BmclApiMirrorTests
{
    [Theory]
    [InlineData("https://launcher.mojang.com/v1/objects/abc/client.jar",
        "https://bmclapi2.bangbang93.com/v1/objects/abc/client.jar")]
    [InlineData("https://launchermeta.mojang.com/mc/game/version_manifest.json",
        "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json")]
    [InlineData("https://piston-meta.mojang.com/v1/packages/xxx/1.json",
        "https://bmclapi2.bangbang93.com/v1/packages/xxx/1.json")]
    [InlineData("https://resources.download.minecraft.net/aa/aaaabbbbccccdddd",
        "https://bmclapi2.bangbang93.com/assets/aa/aaaabbbbccccdddd")]
    [InlineData("https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar",
        "https://bmclapi2.bangbang93.com/maven/org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar")]
    [InlineData("https://maven.fabricmc.net/net/fabricmc/fabric-loader/0.15.0/fabric-loader-0.15.0.jar",
        "https://bmclapi2.bangbang93.com/maven/net/fabricmc/fabric-loader/0.15.0/fabric-loader-0.15.0.jar")]
    [InlineData("https://files.minecraftforge.net/maven/net/minecraftforge/forge/1.20.1-47.2.0/forge-1.20.1-47.2.0-installer.jar",
        "https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/1.20.1-47.2.0/forge-1.20.1-47.2.0-installer.jar")]
    public void TryFindUrl_MirrorEnabled_RewritesToBmclapi(string source, string expected)
    {
        DownloadManager.IsEnableMirror = true;
        try
        {
            Assert.Equal(expected, DownloadManager.BmclApi.TryFindUrl(source));
        }
        finally
        {
            DownloadManager.IsEnableMirror = false;
        }
    }

    [Fact]
    public void TryFindUrl_MirrorDisabled_ReturnsSourceUnchanged()
    {
        DownloadManager.IsEnableMirror = false;
        Assert.Equal(
            "https://launcher.mojang.com/v1/objects/abc/client.jar",
            DownloadManager.BmclApi.TryFindUrl("https://launcher.mojang.com/v1/objects/abc/client.jar"));
    }

    [Fact]
    public void TryFindUrl_NonMirroredDomain_Unchanged()
    {
        DownloadManager.IsEnableMirror = true;
        try
        {
            Assert.Equal(
                "https://example.com/not-mirrored/file.bin",
                DownloadManager.BmclApi.TryFindUrl("https://example.com/not-mirrored/file.bin"));
        }
        finally
        {
            DownloadManager.IsEnableMirror = false;
        }
    }
}
