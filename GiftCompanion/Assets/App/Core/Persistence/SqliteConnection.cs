using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GiftCompanion.Core.Persistence
{
    // One owner serializes this connection. No SQL errors or bound values enter logs.
    internal sealed class SqliteConnection : IDisposable
    {
        private const string Library = "gift_sqlite3";
        private IntPtr handle;
        public SqliteConnection(string path, bool readOnly = false)
        {
            int result = sqlite3_open_v2(Bytes(path), out handle, (readOnly ? 1 : 2 | 4) | 0x10000, IntPtr.Zero);
            if (result == 0) return;
            Dispose();
            throw new IOException($"SQLite open failed ({result}).");
        }
        public void Execute(string sql)
        {
            RequireOpen();
            int result = sqlite3_exec(handle, Bytes(sql), IntPtr.Zero, IntPtr.Zero, out var error);
            if (error != IntPtr.Zero) sqlite3_free(error);
            Check(result, 0);
        }
        public Statement Prepare(string sql)
        {
            RequireOpen();
            int result = sqlite3_prepare_v2(handle, Bytes(sql), -1, out var pointer, IntPtr.Zero);
            if (result != 0 && pointer != IntPtr.Zero) sqlite3_finalize(pointer);
            Check(result, 0);
            return new Statement(pointer);
        }
        public int Version
        {
            get { using var query = Prepare("PRAGMA user_version;"); query.Row(); return (int)query.Integer(0); }
        }
        public void Backup(string path)
        {
            RequireOpen();
            using var destination = new SqliteConnection(path);
            var backup = sqlite3_backup_init(destination.handle, Bytes("main"), handle, Bytes("main"));
            if (backup == IntPtr.Zero) throw new IOException("SQLite backup could not start.");
            int result;
            try { result = sqlite3_backup_step(backup, -1); }
            finally { sqlite3_backup_finish(backup); }
            Check(result, 101);
            // A self-contained snapshot must not ask a read-only validator to create WAL/SHM sidecars.
            destination.Execute("PRAGMA journal_mode=DELETE;");
        }
        private void RequireOpen()
        {
            if (handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(SqliteConnection));
        }
        public void Dispose()
        {
            if (handle == IntPtr.Zero) return;
            sqlite3_close_v2(handle);
            handle = IntPtr.Zero;
        }
        private static void Check(int actual, int expected)
        {
            if (actual != expected) throw new IOException($"SQLite operation failed ({actual}).");
        }
        private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text + "\0");
        internal sealed class Statement : IDisposable
        {
            private IntPtr pointer;
            public Statement(IntPtr pointer) => this.pointer = pointer;
            public void Bind(int index, string text)
            {
                var bytes = Bytes(text ?? throw new ArgumentNullException(nameof(text)));
                Check(sqlite3_bind_text(pointer, index, bytes, bytes.Length - 1, new IntPtr(-1)), 0);
            }
            public bool Next()
            {
                int result = sqlite3_step(pointer);
                if (result == 100) return true;
                Check(result, 101); return false;
            }
            public void Row() { if (!Next()) throw new InvalidDataException("SQLite row is missing."); }
            public void Done() => Check(sqlite3_step(pointer), 101);
            public long Integer(int column) => sqlite3_column_int64(pointer, column);
            public double Real(int column) => sqlite3_column_double(pointer, column);
            public string Text(int column)
            {
                int length = sqlite3_column_bytes(pointer, column);
                var bytes = new byte[length];
                if (length > 0) Marshal.Copy(sqlite3_column_text(pointer, column), bytes, 0, length);
                return Encoding.UTF8.GetString(bytes);
            }
            public void Dispose() { if (pointer != IntPtr.Zero) sqlite3_finalize(pointer); pointer = IntPtr.Zero; }
        }
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2(byte[] path, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr arg, out IntPtr error);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void sqlite3_free(IntPtr memory);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int length, out IntPtr statement, IntPtr tail);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_bind_text(IntPtr statement, int index, byte[] value, int length, IntPtr destructor);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern long sqlite3_column_int64(IntPtr statement, int column);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern double sqlite3_column_double(IntPtr statement, int column);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_bytes(IntPtr statement, int column);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_backup_init(IntPtr destination, byte[] destinationName, IntPtr source, byte[] sourceName);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_backup_step(IntPtr backup, int pages);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_backup_finish(IntPtr backup);
    }
}
