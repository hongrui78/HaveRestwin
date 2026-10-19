using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HaveRestwin;

/// Win32 互操作：最后输入时间（空闲检测）、显示器枚举、系统 DPI。
public static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();

    /// 距最近一次真实键鼠输入的秒数。
    public static double SecondsSinceUserInput()
    {
        var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref lii)) return 0;
        long idleMs = unchecked((long)GetTickCount64() - lii.dwTime);
        return Math.Max(0, idleMs / 1000.0);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly int X => Left;
        public readonly int Y => Top;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags; // 1 = primary
    }

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    public readonly record struct MonitorInfo(RECT Bounds, RECT WorkArea, bool IsPrimary);

    /// 所有显示器的物理像素边界与工作区。
    public static List<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref RECT r, IntPtr d) =>
        {
            var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(h, ref info))
                list.Add(new MonitorInfo(info.rcMonitor, info.rcWork, info.dwFlags == 1));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    /// 系统 DPI（System Aware 下所有窗口共用），失败按 96 处理。
    public static double SystemDpi()
    {
        try
        {
            uint dpi = GetDpiForSystem();
            return dpi == 0 ? 96 : dpi;
        }
        catch (EntryPointNotFoundException)
        {
            return 96;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
