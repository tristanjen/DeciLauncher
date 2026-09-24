// WebViewArgumentsBuilder 测试：
// 1) 恒定下发 --disable-gpu（沿用 5e0f163 的省内存设计，实测与透明合成兼容）；
// 2) 关闭 Chromium 后台组件与 renderer 堆上限恒定下发；
// 3) 缩放必须按 InvariantCulture 格式化（逗号小数点会让 Chromium 解析失败）。

using DeciLauncher;

namespace DeciLauncher.Tests;

public class WebViewArgumentsBuilderTests
{
    [Fact]
    public void Build_AlwaysDisablesGpu()
    {
        var args = WebViewArgumentsBuilder.Build(1.0f);
        Assert.Contains("--disable-gpu", args);
        Assert.Contains("--force-device-scale-factor=1", args);
    }

    [Fact]
    public void Build_DisablesNativeWinOcclusion()
    {
        // 屏幕外起步依赖此开关：否则窗口回来时 Chromium 不产生新帧（全黑）
        var args = WebViewArgumentsBuilder.Build(1.0f);
        Assert.Contains("--disable-features=CalculateNativeWinOcclusion", args);
    }

    [Fact]
    public void Build_KeepsCommonFlags()
    {
        var args = WebViewArgumentsBuilder.Build(1.5f);
        Assert.Contains("--force-device-scale-factor=1.5", args);
        Assert.Contains("--disable-background-networking", args);
        Assert.Contains("--disable-component-update", args);
        Assert.Contains("--disable-default-apps", args);
        Assert.Contains("--js-flags=--max-old-space-size=256", args);
    }

    [Fact]
    public void Build_ScaleUsesInvariantCultureEvenUnderCommaDecimalCulture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // de-DE 用逗号作小数点：若未用 InvariantCulture，会产出 1,25 并让 Chromium 解析失败
            System.Globalization.CultureInfo.CurrentCulture =
                new System.Globalization.CultureInfo("de-DE");
            var args = WebViewArgumentsBuilder.Build(1.25f);
            Assert.Contains("--force-device-scale-factor=1.25", args);
            Assert.DoesNotContain("1,25", args);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
