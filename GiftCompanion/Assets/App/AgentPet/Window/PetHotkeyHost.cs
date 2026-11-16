using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Config;

namespace GiftCompanion.AgentPet.Window
{
    // Thread-owned WM_HOTKEY pump. Registration changes and cleanup use the same thread.
    public sealed class PetHotkeyHost : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct Message { public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam; public uint Time; public int X, Y; public uint Private; }
        private readonly ConcurrentQueue<Action> commands = new ConcurrentQueue<Action>();
        private readonly AutoResetEvent signal = new AutoResetEvent(false);
        private readonly Thread thread;
        private readonly object gate = new object();
        private volatile bool stopping;
        private int registration, pending;
        public bool Consume() => Interlocked.Exchange(ref pending, 0) != 0;
        public PetHotkeyHost() { thread = new Thread(Run) { IsBackground = true, Name = "Gift hotkey" }; thread.Start(); }
        public Task<bool> RegisterAsync(PetHotkey? key)
        {
            var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
            {
              if (stopping) { result.SetResult(false); return result.Task; }
              commands.Enqueue(() =>
              {
                if (!key.HasValue) { if (registration != 0) UnregisterHotKey(IntPtr.Zero, registration); registration = 0; Interlocked.Exchange(ref pending, 0); result.SetResult(true); return; }
                int next = registration == 1 ? 2 : 1;
                bool ok = RegisterHotKey(IntPtr.Zero, next, key.Value.Modifiers | 0x4000, key.Value.Key);
                if (ok) { if (registration != 0) UnregisterHotKey(IntPtr.Zero, registration); registration = next; }
                result.SetResult(ok);
              }); signal.Set();
            }
            return result.Task;
        }
        private void Run()
        {
            PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
            try
            {
                while (!stopping)
                {
                    while (commands.TryDequeue(out var command)) command();
                    while (PeekMessage(out var message, IntPtr.Zero, 0, 0, 1)) if (message.Id == 0x0312 && message.WParam.ToUInt64() == (ulong)registration) Interlocked.Exchange(ref pending, 1);
                    signal.WaitOne(20);
                }
                while (commands.TryDequeue(out var command)) command();
            }
            finally { if (registration != 0) UnregisterHotKey(IntPtr.Zero, registration); }
        }
        public void Dispose() { lock (gate) { if (stopping) return; stopping = true; signal.Set(); } if (thread.Join(1000)) signal.Dispose(); }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, IntPtr window, uint min, uint max, uint remove);
    }
}
