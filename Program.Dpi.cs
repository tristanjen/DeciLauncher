namespace DeciLauncher;

partial class Program
{
    // ===== 窗口设计尺寸常量 =====
    // 窗口基础宽度（100% 缩放时的逻辑像素宽度）
    private const int BaseWindowWidth = 874;
    // 窗口基础高度（100% 缩放时的逻辑像素高度）
    private const int BaseWindowHeight = 524;
    // 标准 DPI 基准值（96 DPI = 100% 缩放）
    private const float StandardDpi = 96f;

    // ===== 窗口尺寸计算 =====

    /// <summary>
    /// 根据缩放比例计算窗口的实际像素尺寸
    /// </summary>
    /// <param name="scale">DPI 缩放比例（1.0 = 100%, 1.25 = 125%）</param>
    /// <returns>缩放后的 (宽度, 高度)</returns>
    private static (int w, int h) GetScaledSize(float scale) =>
        ((int)Math.Round(BaseWindowWidth * scale), (int)Math.Round(BaseWindowHeight * scale));

    // ===== 跨平台 DPI 缩放率检测 =====

    /// <summary>
    /// 获取当前系统的 DPI 缩放比例（跨平台）
    /// Windows: GetDpiForSystem / 96
    /// macOS: 物理像素 / 逻辑点宽度比
    /// Linux: GDK_SCALE 或 QT_SCALE_FACTOR 环境变量
    /// </summary>
    private static float GetSystemScale()
    {
        // Windows：使用系统级 DPI API 获取真实缩放比
        if (OperatingSystem.IsWindows())
            return GetDpiForSystem() / StandardDpi;

        // macOS：通过 CoreGraphics 对比物理像素与逻辑点坐标计算缩放比
        if (OperatingSystem.IsMacOS())
            return GetMacOSScale();

        // Linux：读取桌面环境缩放因子环境变量
        if (OperatingSystem.IsLinux())
            return GetLinuxScale();

        // 未知平台回退到 100% 缩放
        return 1.0f;
    }

    /// <summary>
    /// macOS DPI 缩放率检测
    /// 通过比较显示器物理像素宽度与逻辑点宽度计算 backing scale factor
    /// </summary>
    private static float GetMacOSScale()
    {
        try
        {
            // 获取主显示器 ID
            var display = CGMainDisplayID();
            // 获取物理像素宽度
            var pixelsWide = (double)CGDisplayPixelsWide(display);
            // 获取逻辑点边界（系统坐标空间）
            var bounds = CGDisplayBounds(display);
            // 物理像素 / 逻辑点宽度 = 缩放因子（Retina 为 2.0，非 Retina 为 1.0）
            if (bounds.size.width > 0)
                return (float)(pixelsWide / bounds.size.width);
        }
        // DEBUG 模式下让异常抛出以快速定位问题，RELEASE 模式下静默回退
        catch (Exception) when (!IsDebugMode)
        {
        }

        // 检测失败时回退到 100%
        return 1.0f;
    }

    /// <summary>
    /// Linux DPI 缩放率检测
    /// 优先读取 GTK 的 GDK_SCALE，其次 KDE/Qt 的 QT_SCALE_FACTOR
    /// </summary>
    private static float GetLinuxScale()
    {
        // 读取 GTK 桌面环境的缩放因子
        var gdkScale = Environment.GetEnvironmentVariable("GDK_SCALE");
        if (float.TryParse(gdkScale, out var scale) && scale > 0)
            return scale;

        // 读取 Qt/KDE 桌面环境的缩放因子
        var qtScale = Environment.GetEnvironmentVariable("QT_SCALE_FACTOR");
        if (float.TryParse(qtScale, out scale) && scale > 0)
            return scale;

        // 无法检测时回退到 100%
        return 1.0f;
    }
}
