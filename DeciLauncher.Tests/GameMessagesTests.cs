// GameMessages 单元测试：锁定 game-exited / game-launched 消息的 JSON 形状。
// 这些常量经 window.external 桥发送到前端，前端的 RequestMap/ResponseMap
// 依赖 "type" 字段分发。拼写漂移会导致前端无法识别消息。

using System.Text.Json;
using DeciLauncher;

namespace DeciLauncher.Tests;

public class GameMessagesTests
{
    [Fact]
    public void GameExited_IsValidJson_WithTypeField()
    {
        var json = JsonDocument.Parse(GameMessages.GameExited);
        Assert.Equal("game-exited", json.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void GameLaunched_IsValidJson_WithTypeField()
    {
        var json = JsonDocument.Parse(GameMessages.GameLaunched);
        Assert.Equal("game-launched", json.RootElement.GetProperty("type").GetString());
    }
}
