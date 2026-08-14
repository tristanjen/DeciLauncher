// 游戏生命周期消息常量（前端消息协议 game-exited / game-launched）
// 此前以手拼 JSON 字符串散布在 Program.Launch.cs 各处，统一为常量避免拼写漂移

namespace DeciLauncher;

internal static class GameMessages
{
    /// <summary>游戏进程已退出（或启动已取消/复位）</summary>
    internal const string GameExited = """{"type":"game-exited"}""";

    /// <summary>游戏窗口已出现（启动成功）</summary>
    internal const string GameLaunched = """{"type":"game-launched"}""";
}
