// 游戏进程输出环形缓冲：崩溃分析时提取最近输出附加到提示，
// 覆盖「未生成 crash-report、根因只在游戏日志里」的崩溃场景。
// 由启动流程在每次启动前 Clear，启动期间由输出事件回调 Add。

namespace DeciLauncher;

internal static class GameLogBuffer
{
    // 容量取 200 行：崩溃根因通常在日志尾部，过多行会稀释 crash-analysis 消息的可读性
    private const int Capacity = 200;

    private static readonly object Lock = new();
    private static readonly Queue<string> Lines = new();

    /// <summary>记录一行游戏输出（stdout/stderr）</summary>
    internal static void Add(string line)
    {
        lock (Lock)
        {
            if (Lines.Count >= Capacity) Lines.Dequeue();
            Lines.Enqueue(line);
        }
    }

    /// <summary>每次启动开始时清空，保证缓冲内容属于本次启动</summary>
    internal static void Clear()
    {
        lock (Lock) Lines.Clear();
    }

    /// <summary>取最近 count 行（时间正序；不足时返回全部）</summary>
    internal static string[] Snapshot(int count)
    {
        lock (Lock)
        {
            return Lines.Skip(Math.Max(0, Lines.Count - count)).ToArray();
        }
    }
}
