using System;
using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace GiftCompanion.Core.Config
{
    public static class LocalSecrets
    {
        public static string ReadDeepSeekKey(string userDataDirectory, bool allowEnvironment = true)
        {
            string environment = Environment.GetEnvironmentVariable("GIFT_DEEPSEEK_API_KEY");
            if (allowEnvironment && !string.IsNullOrWhiteSpace(environment)) return environment.Trim();
            string path = Path.Combine(userDataDirectory, "Secrets", "deepseek.key.dpapi");
            AtomicDocument.Recover(path);
            if (!File.Exists(path)) return null;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("The local key is protected by Windows DPAPI.");
            if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Local credential file is too large.");
            byte[] encrypted = File.ReadAllBytes(path);
            if (encrypted.Length > 7 && Encoding.ASCII.GetString(encrypted, 0, 7) == "DPAPI1\n")
                encrypted = Convert.FromBase64String(Encoding.ASCII.GetString(encrypted, 7, encrypted.Length - 7));
            var input = new Blob { Length = encrypted.Length, Data = Marshal.AllocHGlobal(encrypted.Length) };
            Blob output = default;
            try
            {
                Marshal.Copy(encrypted, 0, input.Data, encrypted.Length);
                if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot decrypt the local DeepSeek key for this Windows user.");
                var bytes = new byte[output.Length];
                try
                {
                    Marshal.Copy(output.Data, bytes, 0, bytes.Length);
                    return Encoding.UTF8.GetString(bytes);
                }
                finally { Array.Clear(bytes, 0, bytes.Length); }
            }
            finally
            {
                Marshal.FreeHGlobal(input.Data);
                if (output.Data != IntPtr.Zero)
                {
                    for (int i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0);
                    LocalFree(output.Data);
                }
            }
        }

        public static void SaveDeepSeekKey(string userDataDirectory, string key, System.Threading.CancellationToken ct)
        {
            key = key?.Trim();
            if (string.IsNullOrEmpty(key) || key.Length > 4096 || System.Linq.Enumerable.Any(key, char.IsWhiteSpace) || System.Linq.Enumerable.Any(key, char.IsControl))
                throw new ArgumentException("请输入有效的Key，不能包含空格或换行。");
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new PlatformNotSupportedException("Windows DPAPI is required.");
            byte[] bytes = Encoding.UTF8.GetBytes(key);
            var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
            Blob output = default;
            try
            {
                Marshal.Copy(bytes, 0, input.Data, bytes.Length);
                if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot protect local credential.");
                var cipher = new byte[output.Length]; Marshal.Copy(output.Data, cipher, 0, cipher.Length);
                // Only ciphertext reaches the atomic writer (including its recovery/previous files).
                AtomicDocument.Write(Path.Combine(userDataDirectory, "Secrets", "deepseek.key.dpapi"), "DPAPI1\n" + Convert.ToBase64String(cipher), ct);
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
                for (int i = 0; i < input.Length; i++) Marshal.WriteByte(input.Data, i, 0);
                Marshal.FreeHGlobal(input.Data);
                if (output.Data != IntPtr.Zero) LocalFree(output.Data);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Blob { public int Length; public IntPtr Data; }
        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(ref Blob input, string description, IntPtr entropy,
            IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy,
            IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    }
}
