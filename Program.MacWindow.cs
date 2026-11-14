// macOS 透明窗口补齐：Photino 4.0.16 的 SetTransparent 在 macOS 上是空实现
// （Photino.Mac.mm 的 SetTransparentEnabled 注释为 “Not implemented”），
// 原生窗口创建后通过 Objective-C 运行时设置 NSWindow/WKWebView 的透明属性，
// 让 CSS 的透明背景真正透出桌面。

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DeciLauncher;

[SupportedOSPlatform("macos")]
internal static class MacWindowTransparency
{
    // Objective-C 运行时（modern macOS 由 dyld 共享缓存提供，文件不一定存在于磁盘）
    private const string ObjC = "/usr/lib/libobjc.dylib";

    // id / SEL 在 64 位平台均为指针宽度
    [DllImport(ObjC, CharSet = CharSet.Ansi)]
    private static extern nint objc_getClass(string name);

    [DllImport(ObjC, CharSet = CharSet.Ansi)]
    private static extern nint sel_registerName(string name);

    [DllImport(ObjC)]
    private static extern nint object_getClassName(nint obj);

    // objc_msgSend 的多种签名：AppKit 方法的参数/返回值类型各不相同，必须按实际 ABI 声明。
    // BOOL 在 64 位 macOS 上是 1 字节，故用 byte 而非默认的 4 字节 bool。
    [DllImport(ObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint objc_msgSend(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint objc_msgSend_nint(nint receiver, nint selector, nint arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint objc_msgSend_bool(nint receiver, nint selector, byte arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
    private static extern void objc_msgSend_void_bool(nint receiver, nint selector, byte arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
    private static extern void objc_msgSend_void_id(nint receiver, nint selector, nint arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
    private static extern void objc_msgSend_void_id_id(nint receiver, nint selector, nint arg1, nint arg2);

    [DllImport(ObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern nint objc_msgSend_str(nint receiver, nint selector, string arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
    private static extern byte objc_msgSend_byte_sel(nint receiver, nint selector, nint arg);

    /// <summary>
    /// 将主窗口设为透明：NSWindow 不透明关闭 + 背景透明，WKWebView 背景不绘制。
    /// 必须在原生窗口创建之后、主线程上调用（Program.Window.cs 的 WindowCreated 回调）。
    /// </summary>
    internal static bool TryApply()
    {
        try
        {
            var window = FindMainWindow();
            if (window == 0)
            {
                Log.Warn("[Window] macOS 透明窗口：未找到原生 NSWindow");
                return false;
            }

            var clearColor = objc_msgSend(objc_getClass("NSColor"), Sel("clearColor"));

            // 窗口本身不再绘制背景；关闭系统窗口阴影，避免透明区域外出现矩形阴影
            objc_msgSend_void_bool(window, Sel("setOpaque:"), 0);
            objc_msgSend_void_id(window, Sel("setBackgroundColor:"), clearColor);
            objc_msgSend_void_bool(window, Sel("setHasShadow:"), 0);

            // WKWebView 默认绘制不透明白底：drawsBackground 只能通过 KVC 设置
            // （它没有公开的 setDrawsBackground: 方法，respondsToSelector 会返回 false，
            // 但 KVC 可直接访问底层属性）；macOS 12+ 再清空 underPageBackgroundColor
            var webView = FindWebView(objc_msgSend(window, Sel("contentView")), 0);
            if (webView == 0)
            {
                Log.Warn("[Window] macOS 透明窗口：未找到 WKWebView，网页区域可能仍为白底");
                return false;
            }

            SetValueForKey(webView, "drawsBackground", false);
            if (RespondsTo(webView, Sel("setUnderPageBackgroundColor:")))
                objc_msgSend_void_id(webView, Sel("setUnderPageBackgroundColor:"), clearColor);

            Log.Info("[Window] macOS 透明窗口已启用");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"[Window] macOS 透明窗口设置失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>通过 NSApplication.windows 按标题查找 Photino 主窗口</summary>
    private static nint FindMainWindow()
    {
        var nsApp = objc_msgSend(objc_getClass("NSApplication"), Sel("sharedApplication"));
        var windows = objc_msgSend(nsApp, Sel("windows"));
        var count = objc_msgSend(windows, Sel("count"));
        for (nint i = 0; i < count; i++)
        {
            var window = objc_msgSend_nint(windows, Sel("objectAtIndex:"), i);
            if (NSStringToUtf8(objc_msgSend(window, Sel("title"))) == "Deci Launcher")
                return window;
        }
        return 0;
    }

    /// <summary>递归查找 WKWebView（Photino 把它作为 contentView 的直接子视图）</summary>
    private static nint FindWebView(nint view, int depth)
    {
        if (view == 0 || depth > 3) return 0;

        var className = Marshal.PtrToStringUTF8(object_getClassName(view));
        if (className != null && className.Contains("WKWebView", StringComparison.Ordinal))
            return view;

        var subviews = objc_msgSend(view, Sel("subviews"));
        if (subviews == 0) return 0;

        var count = objc_msgSend(subviews, Sel("count"));
        for (nint i = 0; i < count; i++)
        {
            var found = FindWebView(objc_msgSend_nint(subviews, Sel("objectAtIndex:"), i), depth + 1);
            if (found != 0) return found;
        }
        return 0;
    }

    /// <summary>KVC：setValue:forKey:</summary>
    private static void SetValueForKey(nint obj, string key, bool value)
    {
        var number = objc_msgSend_bool(objc_getClass("NSNumber"), Sel("numberWithBool:"),
            value ? (byte)1 : (byte)0);
        var nsKey = objc_msgSend_str(objc_getClass("NSString"), Sel("stringWithUTF8String:"), key);
        objc_msgSend_void_id_id(obj, Sel("setValue:forKey:"), number, nsKey);
    }

    private static bool RespondsTo(nint obj, nint selector) =>
        objc_msgSend_byte_sel(obj, Sel("respondsToSelector:"), selector) != 0;

    private static string? NSStringToUtf8(nint nsString)
    {
        if (nsString == 0) return null;
        var utf8 = objc_msgSend(nsString, Sel("UTF8String"));
        return utf8 == 0 ? null : Marshal.PtrToStringUTF8(utf8);
    }

    private static nint Sel(string name) => sel_registerName(name);
}
