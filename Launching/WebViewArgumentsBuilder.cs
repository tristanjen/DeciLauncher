// WebView2 启动参数构建（纯函数，便于单元测试）。
// 说明：--disable-gpu（软件渲染）沿用 5e0f163 的省内存设计——本启动器是静态 UI，
// 实测它与 SetTransparent(true) 的透明合成兼容，且与"首帧黑底闪现"无关
// （该闪现的成因是 WebView2 首帧之前的不透明表面，靠"就绪后再显示窗口"解决，见 Program.Window.cs）。

namespace DeciLauncher;

internal static class WebViewArgumentsBuilder
{
    // 所有模式通用的参数：
    // - --disable-features=CalculateNativeWinOcclusion：关闭 Windows 原生遮挡检测。
    //   窗口启动时位于屏幕外（见 Program.Window.cs 的首帧黑底处理），若不关闭该检测，
    //   Chromium 会把"完全不可见"的窗口判定为被遮挡并停止合成，移回屏幕后不会产生新帧
    //   （实测表现为窗口回来时是全黑，且不会自行恢复）；
    // - --disable-gpu：软件渲染（沿用 5e0f163 的省内存设计，实测与透明合成兼容）；
    // - 其余：关闭 Chromium 后台组件，并限制 renderer 的 V8 堆（3 个静态页面 256 MB 绰绰有余）
    private static readonly string[] CommonArguments =
    [
        "--disable-features=CalculateNativeWinOcclusion",
        "--disable-gpu",
        "--disable-background-networking",
        "--disable-component-update",
        "--disable-default-apps",
        "--js-flags=--max-old-space-size=256"
    ];

    /// <summary>
    /// 构建 WebView2 附加启动参数。
    /// 注意：不使用 --metrics-recording-only（其语义是本地记录指标，会把含 ?token=
    /// 的导航 URL 写入本地 EBWebView 数据目录）。
    /// </summary>
    /// <param name="scale">DPI 缩放比例（InvariantCulture 格式化，避免小数点被本地化成逗号导致 Chromium 解析失败）</param>
    internal static string Build(float scale)
    {
        var args = new List<string>
        {
            $"--force-device-scale-factor={scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        };
        args.AddRange(CommonArguments);
        return string.Join(' ', args);
    }
}
