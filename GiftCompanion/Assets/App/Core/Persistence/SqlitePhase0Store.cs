using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace GiftCompanion.Core.Persistence
{
    // Isolated Phase 0 database. The application's RAM service is replaced only after the gate passes.
    public sealed class SqlitePhase0Store : IDisposable
    {
        private const string Library = "gift_sqlite3";
        private readonly object gate = new object();
        private IntPtr database;
        private static readonly IntPtr Transient = new IntPtr(-1);
        public string NativeVersion => Utf8(sqlite3_libversion());

        public sealed class Message
        {
            public long Id { get; internal set; }
            public string SessionId { get; internal set; }
            public string Role { get; internal set; }
            public string Content { get; internal set; }
        }

        public SqlitePhase0Store(string path)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            int result = sqlite3_open_v2(Bytes(fullPath), out database, 2 | 4 | 0x10000, IntPtr.Zero);
            if (result != 0)
            {
                if (database != IntPtr.Zero) { sqlite3_close_v2(database); database = IntPtr.Zero; }
                throw new IOException($"SQLite open failed ({result}).");
            }
            try
            {
                Execute("PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;");
                long version;
                using (var statement = Prepare("PRAGMA user_version;"))
                {
                    StepRow(statement.Pointer);
                    version = sqlite3_column_int64(statement.Pointer, 0);
                }
                if (version > 1) throw new InvalidDataException("SQLite Phase 0 schema is newer than supported.");
                if (version == 0)
                {
                    Execute("BEGIN IMMEDIATE;");
                    try
                    {
                        Execute(@"CREATE TABLE messages (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            session_id TEXT NOT NULL,
                            role TEXT NOT NULL CHECK(role IN ('user','assistant','system','tool')),
                            content TEXT NOT NULL,
                            created_at TEXT NOT NULL);
                            CREATE VIRTUAL TABLE message_fts USING fts5(content,content='messages',content_rowid='id');
                            CREATE TRIGGER messages_ai AFTER INSERT ON messages BEGIN
                              INSERT INTO message_fts(rowid,content) VALUES(new.id,new.content); END;
                            PRAGMA user_version=1;");
                        Execute("COMMIT;");
                    }
                    catch { Execute("ROLLBACK;"); throw; }
                }
            }
            catch { Dispose(); throw; }
        }

        public long SaveMessage(string sessionId, string role, string content, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Session is required.");
            if (role != "user" && role != "assistant" && role != "system" && role != "tool") throw new ArgumentException("Invalid message role.");
            if (content == null) throw new ArgumentNullException(nameof(content));
            lock (gate)
            {
                ct.ThrowIfCancellationRequested();
                using var statement = Prepare("INSERT INTO messages(session_id,role,content,created_at) VALUES(?1,?2,?3,?4);");
                Bind(statement.Pointer, 1, sessionId); Bind(statement.Pointer, 2, role); Bind(statement.Pointer, 3, content);
                Bind(statement.Pointer, 4, DateTimeOffset.UtcNow.ToString("O"));
                ct.ThrowIfCancellationRequested();
                Check(sqlite3_step(statement.Pointer), 101);
                return sqlite3_last_insert_rowid(database);
            }
        }

        public List<Message> ReadMessages(string sessionId) => Read(
            "SELECT id,session_id,role,content FROM messages WHERE session_id=?1 ORDER BY id;", sessionId);

        public List<Message> Search(string query) => Read(
            "SELECT m.id,m.session_id,m.role,m.content FROM message_fts f JOIN messages m ON m.id=f.rowid WHERE message_fts MATCH ?1 ORDER BY m.id;", query);

        private List<Message> Read(string sql, string parameter)
        {
            lock (gate)
            {
                using var statement = Prepare(sql);
                Bind(statement.Pointer, 1, parameter);
                var rows = new List<Message>();
                int result;
                while ((result = sqlite3_step(statement.Pointer)) == 100)
                    rows.Add(new Message { Id = sqlite3_column_int64(statement.Pointer, 0),
                        SessionId = Column(statement.Pointer, 1), Role = Column(statement.Pointer, 2), Content = Column(statement.Pointer, 3) });
                Check(result, 101);
                return rows;
            }
        }

        private Statement Prepare(string sql)
        {
            if (database == IntPtr.Zero) throw new ObjectDisposedException(nameof(SqlitePhase0Store));
            Check(sqlite3_prepare_v2(database, Bytes(sql), -1, out var statement, IntPtr.Zero), 0);
            return new Statement(statement);
        }

        private void Execute(string sql)
        {
            int result = sqlite3_exec(database, Bytes(sql), IntPtr.Zero, IntPtr.Zero, out var error);
            if (error != IntPtr.Zero) sqlite3_free(error);
            Check(result, 0);
        }

        private static void Bind(IntPtr statement, int index, string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            byte[] bytes = Bytes(text);
            Check(sqlite3_bind_text(statement, index, bytes, bytes.Length - 1, Transient), 0);
        }

        private static void StepRow(IntPtr statement) => Check(sqlite3_step(statement), 100);
        private static void Check(int actual, int expected)
        {
            if (actual != expected) throw new IOException($"SQLite operation failed ({actual}).");
        }
        private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text + "\0");
        private static string Utf8(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return string.Empty;
            int length = 0; while (Marshal.ReadByte(pointer, length) != 0) length++;
            var bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
        private static string Column(IntPtr statement, int index)
        {
            IntPtr pointer = sqlite3_column_text(statement, index);
            int length = sqlite3_column_bytes(statement, index);
            if (length == 0) return string.Empty;
            var bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (database == IntPtr.Zero) return;
                int result = sqlite3_close_v2(database); database = IntPtr.Zero;
                Check(result, 0);
            }
        }
        private sealed class Statement : IDisposable
        {
            public IntPtr Pointer { get; }
            public Statement(IntPtr pointer) => Pointer = pointer;
            public void Dispose() => sqlite3_finalize(Pointer);
        }

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2(byte[] path, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_libversion();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr arg, out IntPtr error);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void sqlite3_free(IntPtr memory);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int length, out IntPtr statement, IntPtr tail);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_bind_text(IntPtr statement, int index, byte[] text, int length, IntPtr destructor);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern long sqlite3_last_insert_rowid(IntPtr db);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern long sqlite3_column_int64(IntPtr statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_text(IntPtr statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_bytes(IntPtr statement, int index);
    }
}
