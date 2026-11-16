using System;
using System.IO;

namespace GiftCompanion.Core.Logging
{
    public interface ILogger : IDisposable
    {
        void Info(string eventName);
        void Error(string eventName, Exception exception);
    }

    public sealed class Logger : ILogger
    {
        private readonly object gate = new object();
        private readonly StreamWriter writer;
        private bool disposed;

        public Logger(string logsPath)
        {
            Directory.CreateDirectory(logsPath);
            string filename = $"app-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log";
            writer = new StreamWriter(Path.Combine(logsPath, filename)) { AutoFlush = true };
        }

        public void Info(string eventName) => Write("INFO", eventName);

        // Exception type is safe by default; messages may contain user text or credentials.
        public void Error(string eventName, Exception exception) =>
            Write("ERROR", $"{eventName} exception={exception.GetType().Name}");

        private void Write(string level, string value)
        {
            lock (gate)
            {
                if (disposed) return;
                writer.WriteLine($"{DateTimeOffset.UtcNow:O} [{level}] {value}");
            }
        }

        public void Dispose()
        {
            lock (gate) { if (disposed) return; disposed = true; writer.Dispose(); }
        }
    }
}
