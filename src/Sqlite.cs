using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace T3Usage
{
    internal sealed class ThreadRow
    {
        public string ThreadId;
        public string Title;
        public string ProviderInstanceId;
        public string UpdatedAt;
        public string PayloadJson;
    }

    internal static class Sqlite
    {
        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_OPEN_READONLY = 0x00000001;
        private const int SQLITE_OPEN_NOMUTEX = 0x00008000;

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open_v2(byte[] filename, out IntPtr ppDb, int flags, IntPtr zVfs);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close_v2(IntPtr db);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_busy_timeout(IntPtr db, int ms);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare_v2(IntPtr db, byte[] zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr stmt);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr stmt);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(IntPtr stmt, int iCol);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_bytes(IntPtr stmt, int iCol);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg(IntPtr db);

        private static byte[] ToUtf8NullTerminated(string text)
        {
            int count = Encoding.UTF8.GetByteCount(text);
            byte[] buf = new byte[count + 1];
            Encoding.UTF8.GetBytes(text, 0, text.Length, buf, 0);
            buf[count] = 0;
            return buf;
        }

        private static string ReadColumnUtf8(IntPtr stmt, int col)
        {
            IntPtr ptr = sqlite3_column_text(stmt, col);
            if (ptr == IntPtr.Zero) return string.Empty;
            int len = sqlite3_column_bytes(stmt, col);
            if (len <= 0) return string.Empty;
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        private static string GetErrorMessage(IntPtr db)
        {
            if (db == IntPtr.Zero) return "null db";
            IntPtr ptr = sqlite3_errmsg(db);
            if (ptr == IntPtr.Zero) return "unknown sqlite error";
            int len = 0;
            while (len < 512 && Marshal.ReadByte(ptr, len) != 0) len++;
            if (len == 0) return string.Empty;
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        public static List<ThreadRow> QueryRecentThreads(string dbPath, int limit)
        {
            List<ThreadRow> results = new List<ThreadRow>();
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                return results;

            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                int rc = sqlite3_open_v2(
                    ToUtf8NullTerminated(Path.GetFullPath(dbPath)),
                    out db,
                    SQLITE_OPEN_READONLY | SQLITE_OPEN_NOMUTEX,
                    IntPtr.Zero);

                if (rc != SQLITE_OK || db == IntPtr.Zero)
                {
                    Log.Info("SQLite open failed (" + rc + "): " + GetErrorMessage(db));
                    return results;
                }

                sqlite3_busy_timeout(db, 250);

                // Sort only thread ids and visit stamps, never the potentially large payload.
                string sql =
                    "SELECT thread_id,title,provider_instance_id,updated_at,payload_json FROM orchestration_v2_projection_threads WHERE thread_id IN (" +
                    "SELECT thread_id FROM orchestration_v2_projection_threads WHERE deleted_at IS NULL AND archived_at IS NULL " +
                    "AND thread_id NOT LIKE 'thread:delegated-task:%' AND coalesce(json_extract(payload_json, '$.lineage.relationshipToParent'), '') != 'subagent' " +
                    "ORDER BY coalesce(json_extract(payload_json, '$.lastVisitedAt'),json_extract(payload_json, '$.updatedAt'),updated_at) DESC LIMIT " + limit.ToString() + ")";
                byte[] sqlBytes = ToUtf8NullTerminated(sql);
                rc = sqlite3_prepare_v2(db, sqlBytes, sqlBytes.Length - 1, out stmt, IntPtr.Zero);
                if (rc != SQLITE_OK || stmt == IntPtr.Zero)
                {
                    Log.Info("SQLite prepare failed (" + rc + "): " + GetErrorMessage(db));
                    return results;
                }

                while (sqlite3_step(stmt) == SQLITE_ROW)
                {
                    ThreadRow row = new ThreadRow();
                    row.ThreadId = ReadColumnUtf8(stmt, 0);
                    row.Title = ReadColumnUtf8(stmt, 1);
                    row.ProviderInstanceId = ReadColumnUtf8(stmt, 2);
                    row.UpdatedAt = ReadColumnUtf8(stmt, 3);
                    row.PayloadJson = ReadColumnUtf8(stmt, 4);
                    results.Add(row);
                }
            }
            catch (Exception ex)
            {
                Log.Error("SQLite query exception", ex);
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
                if (db != IntPtr.Zero) sqlite3_close_v2(db);
            }

            return results;
        }

        public static List<string[]> QueryRows(string dbPath, string sql, int colCount, int maxRows)
        {
            List<string[]> rows = new List<string[]>();
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath) || colCount <= 0)
                return rows;

            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                int rc = sqlite3_open_v2(
                    ToUtf8NullTerminated(Path.GetFullPath(dbPath)),
                    out db,
                    SQLITE_OPEN_READONLY | SQLITE_OPEN_NOMUTEX,
                    IntPtr.Zero);
                if (rc != SQLITE_OK || db == IntPtr.Zero)
                    return rows;

                sqlite3_busy_timeout(db, 150);
                byte[] sqlBytes = ToUtf8NullTerminated(sql);
                rc = sqlite3_prepare_v2(db, sqlBytes, sqlBytes.Length - 1, out stmt, IntPtr.Zero);
                if (rc != SQLITE_OK || stmt == IntPtr.Zero)
                {
                    Log.Info("SQLite prepare failed (" + rc + "): " + GetErrorMessage(db));
                    return rows;
                }

                while (rows.Count < maxRows && sqlite3_step(stmt) == SQLITE_ROW)
                {
                    string[] cols = new string[colCount];
                    for (int i = 0; i < colCount; i++)
                        cols[i] = ReadColumnUtf8(stmt, i);
                    rows.Add(cols);
                }
            }
            catch (Exception ex)
            {
                Log.Error("SQLite rows exception", ex);
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
                if (db != IntPtr.Zero) sqlite3_close_v2(db);
            }
            return rows;
        }

        public static string[] QuerySingleRow(string dbPath, string sql, int colCount)
        {
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath) || colCount <= 0)
                return null;

            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                int rc = sqlite3_open_v2(
                    ToUtf8NullTerminated(Path.GetFullPath(dbPath)),
                    out db,
                    SQLITE_OPEN_READONLY | SQLITE_OPEN_NOMUTEX,
                    IntPtr.Zero);

                if (rc != SQLITE_OK || db == IntPtr.Zero)
                    return null;

                sqlite3_busy_timeout(db, 150);

                byte[] sqlBytes = ToUtf8NullTerminated(sql);
                rc = sqlite3_prepare_v2(db, sqlBytes, sqlBytes.Length - 1, out stmt, IntPtr.Zero);
                if (rc != SQLITE_OK || stmt == IntPtr.Zero)
                    return null;

                if (sqlite3_step(stmt) == SQLITE_ROW)
                {
                    string[] cols = new string[colCount];
                    for (int i = 0; i < colCount; i++)
                        cols[i] = ReadColumnUtf8(stmt, i);
                    return cols;
                }
            }
            catch
            {
            }
            finally
            {
                if (stmt != IntPtr.Zero) sqlite3_finalize(stmt);
                if (db != IntPtr.Zero) sqlite3_close_v2(db);
            }
            return null;
        }
    }
}



