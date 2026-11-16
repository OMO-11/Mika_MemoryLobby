using System;
using System.Threading;

namespace GiftCompanion.Bootstrap
{
    public sealed class SingleInstanceGuard : IDisposable
    {
        private readonly Mutex mutex;
        private readonly bool ownsMutex;
        public bool IsPrimary { get; }

        public SingleInstanceGuard(bool enforce)
        {
            if (!enforce) { IsPrimary = true; return; }
            mutex = new Mutex(false, "Local\\GiftCompanion.V1");
            try { ownsMutex = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { ownsMutex = true; }
            IsPrimary = ownsMutex;
        }

        public void Dispose()
        {
            if (ownsMutex) mutex.ReleaseMutex();
            mutex?.Dispose();
        }
    }
}
