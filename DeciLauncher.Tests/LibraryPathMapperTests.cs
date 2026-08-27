// LibraryPathMapper.BuildClassPath / ParseLibraryPaths 单元测试：
// 构建 JVM classpath（vanilla jar + 核心 jar + 全部库去重）与
// 从版本 JSON 提取库文件路径（downloads.artifact.path 优先，name 回退）

using System.Text.Json;
using DeciLauncher;
using MinecraftLaunch.Base.Enums;
using MinecraftLaunch.Base.Models.Game;

namespace DeciLauncher.Tests;

public class LibraryPathMapperBuildClassPathTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _versionsDir;

    public LibraryPathMapperBuildClassPathTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DeciLtp_{Guid.NewGuid():N}");
        _versionsDir = Path.Combine(_tempDir, "versions", "1.0");
        Directory.CreateDirectory(_versionsDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, true);
    }

    private VanillaMinecraftEntry Vanilla(string id = "1.0")
    {
        return new VanillaMinecraftEntry
        {
            Id = id,
            Version = new MinecraftVersion(id, MinecraftVersionType.Release),
            ClientJarPath = Path.Combine(_tempDir, "client.jar"),
            ClientJsonPath = Path.Combine(_tempDir, "1.0.json"),
            AssetIndexJsonPath = "unused.json",
            MinecraftFolderPath = _tempDir,
            ReleaseTime = DateTime.UnixEpoch,
        };
    }

    private void WriteJson(VanillaMinecraftEntry game, string json)
    {
        File.WriteAllText(game.ClientJsonPath!, json);
    }

    [Fact]
    public void BuildClassPath_ClientJarFirst_ThenLibraries()
    {
        var game = Vanilla();
        WriteJson(game, """
            {"libraries":[
              {"name":"org.lwjgl:lwjgl:3.2.1"}
            ]}
            """);

        var cp = LibraryPathMapper.BuildClassPath(game, _tempDir);
        // Windows 用分号、Linux/macOS 用冒号分隔
        var sep = OperatingSystem.IsWindows() ? ';' : ':';
        var parts = cp.Split(sep);

        Assert.Equal(2, parts.Length);
        Assert.Equal(game.ClientJarPath, parts[0]);
        Assert.Equal(Path.Combine(_tempDir, "libraries", "org/lwjgl/lwjgl/3.2.1/lwjgl-3.2.1.jar"), parts[1]);
    }

    [Fact]
    public void ParseLibraryPaths_PrefersDownloadsArtifactPath()
    {
        var game = Vanilla();
        WriteJson(game, """
            {"libraries":[
              {"name":"com.mojang:some:2.0","downloads":{"artifact":{"path":"custom/path/some-2.0.jar"}}}
            ]}
            """);

        var paths = LibraryPathMapper.ParseLibraryPaths(game, _tempDir);
        Assert.Equal(["custom/path/some-2.0.jar"], paths);
    }

    [Fact]
    public void ParseLibraryPaths_NameFallback_WhenNoArtifact()
    {
        var game = Vanilla();
        WriteJson(game, """{"libraries":[{"name":"a.b:c:1.0"}]}""");

        var paths = LibraryPathMapper.ParseLibraryPaths(game, _tempDir);
        Assert.Equal(["a/b/c/1.0/c-1.0.jar"], paths);
    }

    [Fact]
    public void ParseLibraryPaths_DeduplicatesAcrossEntries()
    {
        var game = Vanilla();
        WriteJson(game, """
            {"libraries":[
              {"name":"a.b:c:1.0"},
              {"downloads":{"artifact":{"path":"a/b/c/1.0/c-1.0.jar"}}}
            ]}
            """);

        var paths = LibraryPathMapper.ParseLibraryPaths(game, _tempDir);
        Assert.Equal(["a/b/c/1.0/c-1.0.jar"], paths);
    }

    [Fact]
    public void ParseLibraryPaths_MissingJsonFile_ReturnsEmpty()
    {
        var game = Vanilla();
        Assert.Empty(LibraryPathMapper.ParseLibraryPaths(game, _tempDir));
    }

    [Fact]
    public void ParseLibraryPaths_NoLibrariesKey_ReturnsEmpty()
    {
        var game = Vanilla();
        WriteJson(game, """{"arguments":{"game":["--x"]}}""");
        Assert.Empty(LibraryPathMapper.ParseLibraryPaths(game, _tempDir));
    }
}
