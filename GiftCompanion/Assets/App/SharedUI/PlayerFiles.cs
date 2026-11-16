using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace GiftCompanion.SharedUI
{
    public static class PlayerFiles
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class OpenFileName
        {
            public int size = Marshal.SizeOf(typeof(OpenFileName)); public IntPtr owner, instance;
            public string filter; public IntPtr customFilter; public int maxCustomFilter, filterIndex = 1;
            public StringBuilder file = new StringBuilder(32768); public int maxFile = 32768;
            public IntPtr fileTitle; public int maxFileTitle; public string initialDir, title; public int flags = 0x80000 | 0x1000 | 0x800 | 0x8;
            public short fileOffset, fileExtension; public string defaultExtension; public IntPtr data, hook; public string template;
            public IntPtr reserved; public int reservedSize, flagsEx;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BrowseInfo
        { public IntPtr owner, root, displayName; public string title; public uint flags; public IntPtr callback, parameter; public int image; }
        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetOpenFileNameW([In, Out] OpenFileName value);
        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSaveFileNameW([In, Out] OpenFileName value);
        public static string BackupFile(bool save)
        {
            var dialog = new OpenFileName { owner = GetActiveWindow(), title = save ? "保存资料备份（选择新文件名）" : "选择资料备份", filter = "Mika资料备份\0*.mikabackup\0\0", defaultExtension = "mikabackup" };
            if (save) { dialog.flags = 0x80000 | 0x800 | 0x8 | 0x2; dialog.file.Append("Mika-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".mikabackup"); }
            return (save ? GetSaveFileNameW(dialog) : GetOpenFileNameW(dialog)) ? dialog.file.ToString() : null;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SHBrowseForFolderW(ref BrowseInfo value);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool SHGetPathFromIDListW(IntPtr item, StringBuilder path);
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        public static string MusicFile()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor) return null;
            var dialog = new OpenFileName { owner = GetActiveWindow(), title = "选择要添加的音乐", filter = "音乐文件\0*.mp3;*.ogg;*.wav\0\0" };
            return GetOpenFileNameW(dialog) ? dialog.file.ToString() : null;
        }
        public static string SpaceFolder()
            => SelectFolder("选择 Spine 空间资源文件夹");
        public static string ApplicationFile()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor) return null;
            var dialog = new OpenFileName { owner = GetActiveWindow(), title = "选择应用程序或快捷方式", filter = "应用程序\0*.exe;*.lnk\0\0" };
            return GetOpenFileNameW(dialog) ? dialog.file.ToString() : null;
        }
        public static string SelectFolder(string title)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor) return null;
            IntPtr display = Marshal.AllocHGlobal(32768 * 2), item = IntPtr.Zero;
            try
            {
                var dialog = new BrowseInfo { owner = GetActiveWindow(), title = title, displayName = display, flags = 1 };
                item = SHBrowseForFolderW(ref dialog); if (item == IntPtr.Zero) return null;
                var path = new StringBuilder(32768); return SHGetPathFromIDListW(item, path) ? path.ToString() : null;
            }
            finally { if (item != IntPtr.Zero) Marshal.FreeCoTaskMem(item); Marshal.FreeHGlobal(display); }
        }
        public static void OpenFolder(string path)
        { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Path.GetFullPath(path) + "\"") { UseShellExecute = true }); }
        public static void ReadText(string path)
        { Process.Start(new ProcessStartInfo("notepad.exe", "\"" + Path.GetFullPath(path) + "\"") { UseShellExecute = true }); }
    }
}
