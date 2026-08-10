// P/Invoke 平台调用互操作
using System.Runtime.InteropServices;

namespace DeciLauncher;

partial class Program
{
    // ===== Win32 窗口消息和常量 =====
    // WM_NCLBUTTONDOWN：非客户区左键按下消息，用于启动原生窗口拖拽
    private const uint WM_NCLBUTTONDOWN = 0x00A1;
    // HTCAPTION：标识标题栏区域，配合 WM_NCLBUTTONDOWN 实现系统级窗口拖动
    private const nint HTCAPTION = 2;
    // GWL_STYLE：窗口样式索引（GetWindowLongPtr 参数）
    private const int GWL_STYLE = -16;
    // WS_SYSMENU：启用系统菜单（允许任务栏最小化/恢复操作）
    private const uint WS_SYSMENU = 0x00080000;
    // WS_MINIMIZEBOX：启用最小化按钮功能
    private const uint WS_MINIMIZEBOX = 0x00020000;
    // SWP_NOMOVE：SetWindowPos 不移动窗口位置
    private const uint SWP_NOMOVE = 0x0002;
    // SWP_NOSIZE：SetWindowPos 不改变窗口尺寸
    private const uint SWP_NOSIZE = 0x0001;
    // SWP_NOZORDER：SetWindowPos 不改变窗口 Z 序
    private const uint SWP_NOZORDER = 0x0004;
    // SWP_FRAMECHANGED：SetWindowPos 通知系统窗口样式已更改
    private const uint SWP_FRAMECHANGED = 0x0020;
    // 进程 DPI 感知上下文：系统级 DPI 感知（解除 Windows DPI 虚拟化）
    private const nint DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = -2;

    // ===== Win32 API 声明 =====

    // 获取系统主显示器的 DPI 值（Windows 10 1607+）
    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    // 设置进程的 DPI 感知级别（解除 DPI 虚拟化以获取真实 DPI）
    [DllImport("user32.dll")]
    private static extern nint SetProcessDpiAwarenessContext(nint value);

    // 向指定窗口发送消息（用于原生拖拽等系统操作）
    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    // 获取窗口样式（LONG_PTR 版本，兼容 32/64 位）
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    // 设置窗口样式（LONG_PTR 版本，兼容 32/64 位）
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    // 设置窗口位置（通知系统窗口样式已更改）
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    // 释放当前线程的鼠标捕获（原生拖拽的前置步骤）
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    // 原生消息框（Kestrel 启动失败等致命错误提示）
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);

    // ===== macOS CoreGraphics API 声明 =====

    // 获取主显示器 ID
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern uint CGMainDisplayID();

    // 获取指定显示器的物理像素宽度
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern nint CGDisplayPixelsWide(uint display);

    // CoreGraphics 坐标点结构体（用于边界矩形）
    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint { public double x; public double y; }

    // CoreGraphics 尺寸结构体（宽度和高度）
    [StructLayout(LayoutKind.Sequential)]
    private struct CGSize { public double width; public double height; }

    // CoreGraphics 矩形结构体（原点 + 尺寸 = 边界区域）
    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect { public CGPoint origin; public CGSize size; }

    // 获取指定显示器的逻辑边界矩形（逻辑点坐标）
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern CGRect CGDisplayBounds(uint display);
}
