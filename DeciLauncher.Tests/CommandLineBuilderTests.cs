// CommandLineBuilder 单元测试：MSVCRT 命令行引号规则与引号感知拆分
// 预期行为以抽取前 Program.Launch.cs 的既有实现为准（行为等价重构）

using DeciLauncher;

namespace DeciLauncher.Tests;

public class CommandLineBuilderTests
{
    // ===== QuoteArgument：MSVCRT 引号规则 =====

    [Theory]
    // 无空白字符：原样返回，不做任何转义（即使含引号/反斜杠）
    [InlineData("simple", "simple")]
    [InlineData("", "")]
    [InlineData("x\\y", "x\\y")]
    [InlineData("\"quoted\"", "\"quoted\"")]
    [InlineData("a\"b", "a\"b")]
    public void QuoteArgument_NoWhitespace_ReturnsAsIs(string input, string expected)
    {
        Assert.Equal(expected, CommandLineBuilder.QuoteArgument(input));
    }

    [Theory]
    // 含空白：双引号包裹
    [InlineData("a b", "\"a b\"")]
    // 尾随反斜杠加倍（MSVCRT 结尾 n*2 规则）
    [InlineData("a b\\", "\"a b\\\\\"")]
    // 内嵌引号：前置反斜杠按 n*2+1 转义
    [InlineData("say \"hi\" now", "\"say \\\"hi\\\" now\"")]
    // 反斜杠+引号组合：引号前 3 个反斜杠（n=1 → 2n+1）
    [InlineData("a \\\" b", "\"a \\\\\\\" b\"")]
    // 反斜杠在普通字符前不转义
    [InlineData("c:\\dir with space", "\"c:\\dir with space\"")]
    public void QuoteArgument_EscapingRules(string input, string expected)
    {
        Assert.Equal(expected, CommandLineBuilder.QuoteArgument(input));
    }

    [Fact]
    public void JoinArguments_MixedArgs_QuotesOnlyWhitespaceOnes()
    {
        var result = CommandLineBuilder.JoinArguments(["plain", "has space", "x\\y"]);
        Assert.Equal("plain \"has space\" x\\y", result);
    }

    [Fact]
    public void JoinArguments_EmptyList_ReturnsEmptyString()
    {
        Assert.Equal("", CommandLineBuilder.JoinArguments([]));
    }

    // ===== SplitArgsRespectingQuotes：旧版 minecraftArguments 拆分 =====

    [Theory]
    [InlineData("a b c", new[] { "a", "b", "c" })]
    [InlineData("--arg \"hello world\" plain", new[] { "--arg", "hello world", "plain" })]
    // 引号被剥除，token 内部空格保留
    [InlineData("\"hello world\"", new[] { "hello world" })]
    // 未闭合引号：剩余内容作为一个 token（既有实现行为）
    [InlineData("\"abc def", new[] { "abc def" })]
    [InlineData("  spaced   out  ", new[] { "spaced", "out" })]
    public void SplitArgsRespectingQuotes_Various(string input, string[] expected)
    {
        Assert.Equal(expected, CommandLineBuilder.SplitArgsRespectingQuotes(input));
    }

    [Fact]
    public void SplitArgsRespectingQuotes_EmptyInput_YieldsNothing()
    {
        Assert.Empty(CommandLineBuilder.SplitArgsRespectingQuotes(""));
    }

    // ===== 往返一致性：拆分再拼接不改变语义 =====

    [Theory]
    [InlineData("--arg \"hello world\" plain")]
    [InlineData("a \"b c\" d \"e f\"")]
    public void SplitThenJoin_RoundTrip(string input)
    {
        var tokens = CommandLineBuilder.SplitArgsRespectingQuotes(input).ToArray();
        var joined = CommandLineBuilder.JoinArguments(tokens);
        // 拼接后再次拆分应得到相同 token 序列
        Assert.Equal(tokens, CommandLineBuilder.SplitArgsRespectingQuotes(joined));
    }
}
