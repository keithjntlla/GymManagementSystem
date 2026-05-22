using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace GymManagementSystem
{
    public static class NotificationHelper
    {
        public static string Today => DateTime.Now.ToString("yyyy-MM-dd");
        public static string Yesterday => DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");

        public static Dictionary<string, string> GetLatestNotifiedDates(IEnumerable<string> memberIds)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var ids = new List<string>();
            foreach (var id in memberIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    ids.Add(id);
            }

            if (ids.Count == 0)
                return result;

            using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                foreach (var memberId in ids)
                {
                    string sql = @"SELECT NotifiedDate FROM MemberNotifications
                                   WHERE MemberID = @mid
                                   ORDER BY NotifiedDate DESC, NotificationID DESC
                                   LIMIT 1";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@mid", memberId);
                        object? value = cmd.ExecuteScalar();
                        if (value != null && value != DBNull.Value)
                            result[memberId] = value.ToString() ?? string.Empty;
                    }
                }
            }

            return result;
        }

        public static bool WasNotifiedOnDate(string? lastNotifiedDate, string date)
            => !string.IsNullOrEmpty(lastNotifiedDate)
               && string.Equals(lastNotifiedDate, date, StringComparison.Ordinal);

        public static string BuildNotifiedStatusLabel(string? lastNotifiedDate)
        {
            if (WasNotifiedOnDate(lastNotifiedDate, Today))
                return "(Already Notified Today)";
            if (WasNotifiedOnDate(lastNotifiedDate, Yesterday))
                return "(Notified Yesterday)";
            return string.Empty;
        }

        public static void RecordNotification(string memberId)
        {
            using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                string sql = @"INSERT INTO MemberNotifications (MemberID, NotifiedDate)
                               VALUES (@mid, @date)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@mid", memberId);
                    cmd.Parameters.AddWithValue("@date", Today);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void RecordNotifications(IEnumerable<string> memberIds)
        {
            using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    string sql = @"INSERT INTO MemberNotifications (MemberID, NotifiedDate)
                                   VALUES (@mid, @date)";
                    foreach (var memberId in memberIds)
                    {
                        if (string.IsNullOrWhiteSpace(memberId))
                            continue;

                        using (var cmd = new SQLiteCommand(sql, conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@mid", memberId);
                            cmd.Parameters.AddWithValue("@date", Today);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                }
            }
        }
    }

}
