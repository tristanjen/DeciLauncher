// ArgumentTemplateEngine / LibraryPathMapper 单元测试：rules 匹配、参数读取、Maven 路径转换
// 预期行为以抽取前 Program.Launch.cs 的既有实现为准（行为等价重构）

using System.Text.Json;
using DeciLauncher;

namespace DeciLauncher.Tests;

public class RulesAllowTests
{
    private static bool Rules(string json) =>
        ArgumentTemplateEngine.RulesAllow(JsonDocument.Parse(json).RootElement);

    private static string CurrentOs =>
        OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsMacOS() ? "osx" : "linux";

    [Fact]
    public void SingleAllow_ReturnsTrue()
    {
        Assert.True(Rules("""[{"action":"allow"}]"""));
    }

    [Fact]
    public void SingleDisallow_ReturnsFalse()
    {
        Assert.False(Rules("""[{"action":"disallow"}]"""));
    }

    [Fact]
    public void AllowThenDisallow_ReturnsFalse()
    {
        // 规则按顺序覆盖
        Assert.False(Rules("""[{"action":"allow"},{"action":"disallow"}]"""));
    }

    [Fact]
    public void DisallowThenAllow_ReturnsTrue()
    {
        Assert.True(Rules("""[{"action":"disallow"},{"action":"allow"}]"""));
    }

    [Fact]
    public void FeaturesRule_IsSkipped()
    {
        // features 规则由启动器显式控制，不自动启用 → allow 不生效
        Assert.False(Rules("""[{"features":{"is_demo_user":true},"action":"allow"}]"""));
    }

    [Fact]
    public void OsRule_MatchingCurrentOs_Applies()
    {
        Assert.True(Rules($$"""[{"os":{"name":"{{CurrentOs}}"},"action":"allow"}]"""));
    }

    [Fact]
    public void OsRule_NonMatchingOs_Skipped()
    {
        // 不匹配的 os 规则被跳过：allow 不生效 → false；disallow 也不生效 → 初始 false
        Assert.False(Rules("""[{"os":{"name":"__no_such_os__"},"action":"allow"}]"""));
        Assert.False(Rules("""[{"os":{"name":"__no_such_os__"},"action":"disallow"}]"""));
    }

    [Fact]
    public void OsRule_WithMismatchedArch_Skipped()
    {
        Assert.False(Rules("""[{"os":{"name":"__no_such_os__","arch":"x86"},"action":"allow"}]"""));
    }

    [Fact]
    public void EmptyRules_ReturnsFalse()
    {
        Assert.False(Rules("[]"));
    }
}

public class ReadVersionArgsTests
{
    [Fact]
    public void ReadJvmArgs_ReadsJvmArray()
    {
        var args = new List<string>();
        ArgumentTemplateEngine.ReadJvmArgs(
            """{"arguments":{"jvm":["-Xmx1G","-Dfoo=bar"]}}""", args);
        Assert.Equal(["-Xmx1G", "-Dfoo=bar"], args);
    }

    [Fact]
    public void ReadGameArgs_ModernFormat_WithRules()
    {
        var args = new List<string>();
        ArgumentTemplateEngine.ReadGameArgs(
            """
            {"arguments":{"game":[
                {"value":["--allowed-array"]},
                {"rules":[{"action":"allow"}],"value":"--allowed"},
                {"rules":[{"action":"disallow"}],"value":"--disallowed"},
                "--plain"
            ]}}
            """, args);
        // value 为数组时逐项展开；rules 不满足时跳过
        Assert.Equal(["--allowed-array", "--allowed", "--plain"], args);
    }

    [Fact]
    public void ReadGameArgs_LegacyFormat_QuotedTokensStayTogether()
    {
        var args = new List<string>();
        ArgumentTemplateEngine.ReadGameArgs(
            """{"minecraftArguments":"--width 854 --title \"My World\""}""", args);
        Assert.Equal(["--width", "854", "--title", "My World"], args);
    }

    [Fact]
    public void ReadGameArgs_MissingArguments_AddsNothing()
    {
        var args = new List<string>();
        ArgumentTemplateEngine.ReadGameArgs("""{"mainClass":"net.minecraft.client.main.Main"}""", args);
        Assert.Empty(args);
    }

    [Fact]
    public void ReadGameArgs_InvalidJson_IsSwallowed()
    {
        // 既有行为：解析失败吞掉并记录，不抛出
        var args = new List<string> { "keep" };
        ArgumentTemplateEngine.ReadGameArgs("not json", args);
        Assert.Equal(["keep"], args);
    }
}

public class LibraryNameToPathTests
{
    [Theory]
    [InlineData("org.lwjgl:lwjgl:3.2.1",
        "org/lwjgl/lwjgl/3.2.1/lwjgl-3.2.1.jar")]
    [InlineData("org.lwjgl:lwjgl:3.2.1:natives-windows",
        "org/lwjgl/lwjgl/3.2.1/lwjgl-3.2.1-natives-windows.jar")]
    [InlineData("com.example:mod:1.0:a:b:c",
        "com/example/mod/1.0/mod-1.0-a-b-c.jar")]
    [InlineData("no-group", "no-group.jar")]
    [InlineData("group:artifact", "group:artifact.jar")]
    public void LibraryNameToPath_Various(string name, string expected)
    {
        Assert.Equal(expected, LibraryPathMapper.LibraryNameToPath(name));
    }
}
