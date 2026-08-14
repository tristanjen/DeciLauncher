// 崩溃原因 → 中文解释映射测试（todolist #7）
// 锁定 CrashReasonMapper 覆盖全部 CrashReasons 枚举值且关键原因有明确解释

using DeciLauncher;
using MinecraftLaunch.Base.Enums;

namespace DeciLauncher.Tests;

public class CrashReasonMapperTests
{
    private static string? Map(CrashReasons r) => CrashReasonMapper.Map(r, (zh, _) => zh);

    [Theory]
    [InlineData(CrashReasons.InsufficientMemory)]
    [InlineData(CrashReasons.GraphicsCardDoesNotSupportOpenGL)]
    [InlineData(CrashReasons.ModCausedGameCrash)]
    [InlineData(CrashReasons.ModLoaderError)]
    [InlineData(CrashReasons.ModInitializationFailed)]
    [InlineData(CrashReasons.ModMixinFailed)]
    [InlineData(CrashReasons.ModInstalledRepeatedly)]
    [InlineData(CrashReasons.TooManyModsExceededIdLimit)]
    [InlineData(CrashReasons.ModFileDecompressed)]
    [InlineData(CrashReasons.ModConfigCausedGameCrash)]
    [InlineData(CrashReasons.JavaVersionTooHigh)]
    [InlineData(CrashReasons.UnsupportedJavaClassVersionError)]
    [InlineData(CrashReasons.LowVersionForgeIncompatibleWithHighVersionJava)]
    [InlineData(CrashReasons.UsingJDK)]
    [InlineData(CrashReasons.UsingOpenJ9)]
    [InlineData(CrashReasons.Using32BitJavaCausedInsufficientJVMMemory)]
    [InlineData(CrashReasons.OptiFineIncompatibleWithForge)]
    [InlineData(CrashReasons.OptiFineCausedWorldLoadingFailure)]
    [InlineData(CrashReasons.MultipleForgeInVersionJson)]
    [InlineData(CrashReasons.PlayerTriggeredDebugCrash)]
    [InlineData(CrashReasons.ShaderOrResourcePackCausedOpenGL1282Error)]
    [InlineData(CrashReasons.TextureTooLargeOrInsufficientGraphicsConfig)]
    [InlineData(CrashReasons.FileOrContentCheckFailed)]
    [InlineData(CrashReasons.ForgeError)]
    [InlineData(CrashReasons.FabricError)]
    [InlineData(CrashReasons.FabricErrorWithSolution)]
    [InlineData(CrashReasons.SpecificBlockCausedCrash)]
    [InlineData(CrashReasons.SpecificEntityCausedCrash)]
    [InlineData(CrashReasons.UnableToLoadTexture)]
    public void Map_KnownReasons_ReturnsExplanation(CrashReasons reason)
    {
        var text = Map(reason);
        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public void Map_CoversEveryEnumValue_WithoutThrowing()
    {
        foreach (var value in Enum.GetValues<CrashReasons>())
        {
            // 每个枚举值要么有解释，要么显式返回 null（未知原因静默）
            _ = Map(value);
        }
    }

    [Fact]
    public void Map_InsufficientMemory_MentionsMemory()
    {
        Assert.Contains("内存", Map(CrashReasons.InsufficientMemory));
    }
}
