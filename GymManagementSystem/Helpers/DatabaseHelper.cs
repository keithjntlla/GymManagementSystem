using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SQLite;
using System.IO;

namespace GymManagementSystem
{
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

                // Table for Users (for login)
                string usersTable = @"CREATE TABLE IF NOT EXISTS Users (
                UserID TEXT PRIMARY KEY,
                Username TEXT UNIQUE NOT NULL,
                Password TEXT NOT NULL,
                Role TEXT,
                CreatedDate TEXT
            );";

                using (var cmd = new SQLiteCommand(usersTable, conn))
                    cmd.ExecuteNonQuery();

                // Check if default users exist, if not, insert them
                string checkUsersSql = "SELECT COUNT(*) FROM Users";
                using (var cmd = new SQLiteCommand(checkUsersSql, conn))
                {
                    int userCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    if (userCount == 0)
                    {
                        string insertUsersSql = @"INSERT INTO Users (UserID, Username, Password, Role, CreatedDate) VALUES 
                            ('USR001', 'admin', 'admin123', 'Administrator', @date),
                            ('USR002', 'staff', 'staff123', 'Staff', @date)";
                        using (var insertCmd = new SQLiteCommand(insertUsersSql, conn))
                        {
                            insertCmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            insertCmd.ExecuteNonQuery();
                        }
                    }
                }
                
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

                // Table for Payments
                string paymentTable = @"CREATE TABLE IF NOT EXISTS Payments (
                PaymentID INTEGER PRIMARY KEY AUTOINCREMENT,
                MemberID TEXT,
                MemberName TEXT,
                AmountPaid REAL,
                TotalAmount REAL,
                Change REAL,
                PaymentMode TEXT,
                MembershipType TEXT,
                DateOfTransaction TEXT,
                NewExpiryDate TEXT,
                FOREIGN KEY(MemberID) REFERENCES Members(MemberID)
            );";

                using (var cmd = new SQLiteCommand(memberTable, conn))
                    cmd.ExecuteNonQuery();

                using (var cmd = new SQLiteCommand(paymentTable, conn))
                    cmd.ExecuteNonQuery();

                // Table for Attendance - Updated to store full datetime and check-out time
                string attendanceTable = @"CREATE TABLE IF NOT EXISTS Attendance (
                AttendanceID INTEGER PRIMARY KEY AUTOINCREMENT,
                MemberID TEXT,
                CheckInTime TEXT,
                CheckInDate TEXT,
                CheckOutTime TEXT,
                FOREIGN KEY(MemberID) REFERENCES Members(MemberID)
            );";

                using (var cmd = new SQLiteCommand(attendanceTable, conn))
                    cmd.ExecuteNonQuery();
            }
        }

    public static void RefreshMemberStatuses()
    {
        using (var conn = new SQLiteConnection(ConnectionString))
        {
            conn.Open();
            // Automatically set status to Expired if current date > ExpiryDate
            // ExpiryDate format is expected to be yyyy-MM-dd for SQLite Date() function to work correctly
            // However, the UI shows M/d/yyyy. We should store in yyyy-MM-dd for DB logic.
            string updateSql = "UPDATE Members SET Status = 'Expired' WHERE ExpiryDate != '-' AND ExpiryDate != '' AND Date(ExpiryDate) < Date('now') AND Status = 'Active'";
            using (var cmd = new SQLiteCommand(updateSql, conn))
                cmd.ExecuteNonQuery();
        }
    }
}
}
