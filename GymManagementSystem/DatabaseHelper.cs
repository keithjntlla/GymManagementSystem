using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SQLite;
using System.IO;

public static class DatabaseHelper
{
    private const string dbName = "GymManagement.db";
    public static string ConnectionString = $"Data Source={dbName};Version=3;";

    public static void InitializeDatabase()
    {
        if (!File.Exists(dbName)) SQLiteConnection.CreateFile(dbName);

        using (var conn = new SQLiteConnection(ConnectionString))
        {
            conn.Open();
            // Table for Members
            string memberTable = @"CREATE TABLE IF NOT EXISTS Members (
                MemberID TEXT PRIMARY KEY,
                FullName TEXT,
                Phone TEXT,
                Gender TEXT,
                DateJoined TEXT,
                ExpiryDate TEXT,
                Status TEXT,
                PhotoPath TEXT
            );";

            using (var cmd = new SQLiteCommand(memberTable, conn))
                cmd.ExecuteNonQuery();
        }
    }

    public static void RefreshMemberStatuses()
    {
        using (var conn = new SQLiteConnection(ConnectionString))
        {
            conn.Open();
            // Automatically set status to Expired if current date > ExpiryDate
            string updateSql = "UPDATE Members SET Status = 'Expired' WHERE ExpiryDate != '-' AND Date(ExpiryDate) < Date('now') AND Status = 'Active'";
            using (var cmd = new SQLiteCommand(updateSql, conn))
                cmd.ExecuteNonQuery();
        }
    }
}