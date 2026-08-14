// Windows 命令行参数拼接与拆分（从 Program.Launch.cs 抽取的纯函数，逻辑未变）

namespace DeciLauncher;

/// <summary>
/// 命令行参数工具：按 MSVCRT 规则拼接/拆分参数字符串。
/// 原为 Program 的私有静态方法，抽取为独立静态类以便单元测试。
/// </summary>
internal static class CommandLineBuilder
{
    /// <summary>
    /// 按 MSVCRT 命令行规则将参数列表拼接为单个命令行字符串：
    /// 含空格的参数用双引号包裹，并正确转义反斜杠与引号，
    /// 避免含空格路径（classpath/natives/game_directory 等）被 JVM 拆成多个参数
    /// </summary>
    internal static string JoinArguments(IEnumerable<string> arguments) =>
        string.Join(' ', arguments.Select(QuoteArgument));

    /// <summary>
    /// 单个参数的 MSVCRT 引号规则：无空白字符则原样返回；
    /// 否则用双引号包裹，并把尾随反斜杠与内嵌引号按 n*2+1 规则转义
    /// </summary>
    internal static string QuoteArgument(string arg)
    {
        if (arg.Length == 0 || !arg.Any(char.IsWhiteSpace))
            return arg;

        var sb = new System.Text.StringBuilder(arg.Length + 2);
        sb.Append('"');
        int backslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }
            if (c == '"')
            {
                sb.Append('\\', backslashes * 2 + 1);
                sb.Append('"');
                backslashes = 0;
                continue;
            }
            sb.Append('\\', backslashes);
            backslashes = 0;
            sb.Append(c);
        }
        sb.Append('\\', backslashes * 2);
        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>
    /// 引号感知的空白拆分：旧版 minecraftArguments 单行字符串中，
    /// 双引号包裹的 token 内部空格不参与拆分，引号本身被剥除（由 JoinArguments 重新加回）
    /// </summary>
    internal static IEnumerable<string> SplitArgsRespectingQuotes(string input)
    {
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (var ch in input)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                continue;
            }
            current.Append(ch);
        }
        if (current.Length > 0)
            yield return current.ToString();
    }
}
