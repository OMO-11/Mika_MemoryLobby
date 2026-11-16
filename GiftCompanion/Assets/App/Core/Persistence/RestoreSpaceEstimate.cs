using System;
using System.IO;

namespace GiftCompanion.Core.Persistence
{
    public sealed class RestoreSpaceEstimate
    {
        public string Volume { get; set; }
        public long CurrentBytes { get; set; }
        public long StagedBytes { get; set; }
        public long RequiredBytes { get; set; }
        public long? AvailableBytes { get; set; }
        public string Description => $"资料盘 {Volume}：当前资料约{CurrentBytes / 1048576d:F1} MiB；本次暂存约{StagedBytes / 1048576d:F1} MiB。\n" +
            $"继续恢复额外预留约{RequiredBytes / 1048576d:F1} MiB（含原资料副本、恢复前备份及写入余量）。" +
            (AvailableBytes.HasValue ? $"可用{AvailableBytes.Value / 1048576d:F1} MiB。" : "暂时无法确认可用空间。") +
            "\n这是保守估算，执行时仍会检查写入失败；所选备份原文件不移动，安装环境不复制。";
        public void EnsureAvailable()
        {
            if (!AvailableBytes.HasValue) throw new InvalidDataException("无法确认资料盘剩余空间。请检查磁盘连接与权限，再预览恢复；当前资料未替换。");
            if (AvailableBytes.Value < RequiredBytes) throw new InvalidDataException("资料盘剩余空间不足。请释放空间后重新预览恢复；当前资料未替换。\n" + Description);
        }
        public static RestoreSpaceEstimate Calculate(long current, long staged, long database, long? available, string volume, bool includeBackup = true)
        {
            if (current < 0 || staged < 0 || database < 0) throw new ArgumentOutOfRangeException();
            return new RestoreSpaceEstimate { Volume = volume, CurrentBytes = current, StagedBytes = staged, AvailableBytes = available,
                RequiredBytes = checked(current + staged + (includeBackup ? current + database : 0) + 64L * 1048576) };
        }
        internal static long? Available(string path)
        {
            try { var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))); return drive.IsReady ? drive.AvailableFreeSpace : (long?)null; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { return null; }
        }
    }
}
