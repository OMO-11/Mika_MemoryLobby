using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GiftCompanion.AgentPet.Window
{
    // Only our existing Unity window is moved/styled. No global keyboard/mouse hooks.
    public static class PetNative
    {
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }
        [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct Monitor { public int Size; public Rect Bounds, Work; public uint Flags; }
        public static IntPtr Handle
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return Process.GetCurrentProcess().MainWindowHandle;
#else
                return IntPtr.Zero;
#endif
            }
        }
        private readonly struct PhysicalCoordinates : IDisposable
        {
            private readonly IntPtr previous;
            public PhysicalCoordinates(bool _) { previous = SetThreadDpiAwarenessContext(new IntPtr(-4)); }
            public void Dispose() { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
        }
        public static Rect Bounds { get { using var dpi = new PhysicalCoordinates(true); GetWindowRect(Handle, out var rect); return rect; } }
        public static Rect WorkArea
        {
            get { using var dpi = new PhysicalCoordinates(true); var monitor = new Monitor { Size = Marshal.SizeOf<Monitor>() }; GetMonitorInfo(MonitorFromWindow(Handle, 2), ref monitor); return monitor.Work; }
        }
        public static Point Cursor { get { using var dpi = new PhysicalCoordinates(true); GetCursorPos(out var point); return point; } }
        public static bool LeftDown => (GetAsyncKeyState(1) & 0x8000) != 0;
        public static UnityEngine.Vector2 ClientCursor() { using var dpi = new PhysicalCoordinates(true); GetCursorPos(out var p); ScreenToClient(Handle, ref p); GetClientRect(Handle, out var rect); return new UnityEngine.Vector2(p.X * UnityEngine.Screen.width / (float)Math.Max(1,rect.Width), UnityEngine.Screen.height - p.Y * UnityEngine.Screen.height / (float)Math.Max(1,rect.Height)); }
        public static bool PointerIsOurs() { using var dpi = new PhysicalCoordinates(true); GetCursorPos(out var p); return GetAncestor(WindowFromPoint(p),2) == Handle; }
        public static void Move(int left, int top) { using var dpi = new PhysicalCoordinates(true); SetWindowPos(Handle, IntPtr.Zero, left, top, 0, 0, 0x15); }
        public static void SetClientSize(int width, int height)
        {
            using var dpi = new PhysicalCoordinates(true);
            GetClientRect(Handle, out var client); var outer = Bounds;
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, width + outer.Width - client.Width, height + outer.Height - client.Height, 0x16);
        }
        public static void Clamp(bool reset)
        {
            if (Handle == IntPtr.Zero) return;
            var rect = Bounds; var work = WorkArea;
            int x = reset ? work.Right - rect.Width - 22 : rect.Left, y = reset ? work.Bottom - rect.Height - 20 : rect.Top;
            Move(Math.Max(work.Left, Math.Min(x, work.Right - rect.Width)), Math.Max(work.Top, Math.Min(y, work.Bottom - rect.Height)));
        }
        public static void InputFocus(bool active)
        {
            if (Handle == IntPtr.Zero) return;
            long style = GetWindowLongPtr(Handle, -20).ToInt64();
            SetWindowLongPtr(Handle, -20, new IntPtr(active ? style & ~0x08000000L : style | 0x08000000L));
            if (active) SetForegroundWindow(Handle);
        }
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr window, ref Point point);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window,uint flags);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref Monitor info);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    }
}
