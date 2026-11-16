using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using GiftCompanion.Core.Agent;
using Newtonsoft.Json.Linq;

namespace GiftCompanion.AgentPet.Tools
{
    public sealed partial class WindowsAgentPlatform
    {
        private delegate bool EnumWindow(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        private bool TryVisibleWindow(string id, out IntPtr window)
        {
            window = IntPtr.Zero;
            if (id == null || !visibleWindows.TryGetValue(id, out var saved) || !IsWindow(saved.Window) || !IsWindowVisible(saved.Window)) return false;
            GetWindowThreadProcessId(saved.Window, out uint process);
            if (process != saved.Process) return false; window = saved.Window; return true;
        }
        private AgentResult RunningApps()
        {
            visibleWindows.Clear(); var apps = new JArray();
            EnumWindows((window, _) =>
            {
                if (apps.Count >= 40) return false;
                if (!IsWindowVisible(window)) return true;
                GetWindowThreadProcessId(window, out uint id);
                try
                {
                    using var process = System.Diagnostics.Process.GetProcessById((int)id);
                    if (process.MainWindowHandle != window || process.Id == System.Diagnostics.Process.GetCurrentProcess().Id) return true;
                    string key = window.ToInt64().ToString(); visibleWindows[key] = (window, id);
                    apps.Add(new JObject { ["name"] = process.ProcessName, ["window_id"] = key });
                }
                catch (ArgumentException) { } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
                return true;
            }, IntPtr.Zero);
            return AgentResult.Ok("已查看当前可见的应用窗口。", new JObject { ["apps"] = apps, ["needs_followup"] = true, ["untrusted_data"] = true });
        }
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")] private class ShellLink { }
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, IntPtr findData, uint flags);
            void GetIDList(out IntPtr item); void SetIDList(IntPtr item);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int length);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int length);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int length);
        }
        public static string ResolveShortcut(string path)
        {
            path = AgentPaths.LocalPath(path); AgentPaths.NoLinks(path);
            if (!Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) return path;
            object link = new ShellLink();
            try
            {
                ((IPersistFile)link).Load(path, 0);
                var arguments = new StringBuilder(8192); ((IShellLink)link).GetArguments(arguments, arguments.Capacity);
                if (arguments.Length > 0) throw new ArgumentException("这个快捷方式带有启动参数，暂时不能登记；请选择实际的程序文件。");
                var target = new StringBuilder(32768); ((IShellLink)link).GetPath(target, target.Capacity, IntPtr.Zero, 4);
                string result = AgentPaths.LocalPath(target.ToString());
                if (!AgentPaths.IsApplication(result) || !Path.GetExtension(result).Equals(".exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("这个快捷方式没有指向可直接打开的应用程序。");
                AgentPaths.NoLinks(result); return result;
            }
            finally { Marshal.FinalReleaseComObject(link); }
        }
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] private class DeviceEnumerator { }
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IntPtr collection);
            [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        }
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        private interface IMMDevice { [PreserveSig] int Activate(ref Guid id, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint); }
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        private interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr callback);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr callback);
            [PreserveSig] int GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
            [PreserveSig] int GetMasterVolumeLevel(out float level);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
            [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
        }
        private static (int Percent, bool Muted) ReadVolume() => WithVolume(endpoint =>
        {
            Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out float level)); Marshal.ThrowExceptionForHR(endpoint.GetMute(out bool muted));
            return ((int)Math.Round(level * 100), muted);
        });
        private static AgentResult ChangeVolume(int? percent, bool? muted) => WithVolume(endpoint =>
        {
            Guid context = Guid.Empty;
            if (percent.HasValue) Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar(percent.Value / 100f, ref context));
            if (muted.HasValue) Marshal.ThrowExceptionForHR(endpoint.SetMute(muted.Value, ref context));
            Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out float level)); Marshal.ThrowExceptionForHR(endpoint.GetMute(out bool actualMute));
            int actual = (int)Math.Round(level * 100);
            if (percent.HasValue && Math.Abs(actual - percent.Value) > 1 || muted.HasValue && actualMute != muted.Value) return AgentResult.Fail("AudioNotVerified", "系统音量的实际结果没有符合预期，请手动检查。");
            return AgentResult.Ok("已确认系统音量为 " + actual + "%" + (actualMute ? "，当前静音。" : "，当前未静音。"), new JObject { ["percent"] = actual, ["muted"] = actualMute, ["verified"] = true });
        });
        private static T WithVolume<T>(Func<IAudioEndpointVolume, T> action)
        {
            object enumerator = new DeviceEnumerator(), endpoint = null; IMMDevice device = null;
            try
            {
                Marshal.ThrowExceptionForHR(((IMMDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0, 0, out device));
                Guid id = typeof(IAudioEndpointVolume).GUID; Marshal.ThrowExceptionForHR(device.Activate(ref id, 23, IntPtr.Zero, out endpoint)); return action((IAudioEndpointVolume)endpoint);
            }
            finally { if (endpoint != null) Marshal.FinalReleaseComObject(endpoint); if (device != null) Marshal.FinalReleaseComObject(device); Marshal.FinalReleaseComObject(enumerator); }
        }
    }
}
