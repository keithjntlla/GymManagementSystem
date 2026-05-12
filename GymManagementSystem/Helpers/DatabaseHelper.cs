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

                string ratesTable = @"CREATE TABLE IF NOT EXISTS Rates (
                        RateID INTEGER PRIMARY KEY AUTOINCREMENT,
                        PlanName TEXT UNIQUE NOT NULL,
                        Price REAL NOT NULL,
                        DurationDays INTEGER NOT NULL,
                        IsArchived INTEGER NOT NULL DEFAULT 0
                    );";

                using (var cmd = new SQLiteCommand(ratesTable, conn))
                    cmd.ExecuteNonQuery();

                // Seed default rates if table is empty
                string checkRatesSql = "SELECT COUNT(*) FROM Rates";
                using (var cmd = new SQLiteCommand(checkRatesSql, conn))
                {
                    int rateCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    if (rateCount == 0)
                    {
                        string insertRatesSql = @"INSERT INTO Rates (PlanName, Price, DurationDays) VALUES 
                                ('Daily', 90.0, 1),
                                ('Weekly', 320.0, 7),
                                ('Half-Month', 550.0, 15),
                                ('Monthly', 900.0, 30),
                                ('Yearly', 9600.0, 365)";
                        using (var insertCmd = new SQLiteCommand(insertRatesSql, conn))
                        {
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

        /// <summary>
        /// Migrates existing Rates table to add IsArchived column if it doesn't exist yet.
        /// Safe to call on every startup.
        /// </summary>
        public static void MigrateRatesTable()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                // Check if IsArchived column already exists
                string checkCol = "PRAGMA table_info(Rates)";
                bool hasArchived = false;
                using (var cmd = new SQLiteCommand(checkCol, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader["name"]?.ToString() == "IsArchived")
                        {
                            hasArchived = true;
                            break;
                        }
                    }
                }
                if (!hasArchived)
                {
                    string alter = "ALTER TABLE Rates ADD COLUMN IsArchived INTEGER NOT NULL DEFAULT 0";
                    using (var cmd = new SQLiteCommand(alter, conn))
                        cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Returns true if any Active member has a payment record with the given plan name.
        /// </summary>
        public static bool HasActiveMembers(string planName)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = @"
                    SELECT COUNT(*) FROM Payments p
                    INNER JOIN Members m ON p.MemberID = m.MemberID
                    WHERE p.MembershipType = @planName
                      AND m.Status = 'Active'";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@planName", planName);
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
        }

        /// <summary>
        /// Marks a plan as archived (IsArchived = 1) instead of deleting it.
        /// </summary>
        public static void ArchivePlan(int rateId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = "UPDATE Rates SET IsArchived = 1 WHERE RateID = @id";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", rateId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Restores an archived plan back to active.
        /// </summary>
        public static void RestorePlan(int rateId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = "UPDATE Rates SET IsArchived = 0 WHERE RateID = @id";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", rateId);
                    cmd.ExecuteNonQuery();
                }
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