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
        public static event Action? ProfileUpdated;

        public static void InitializeDatabase()
        {
            if (!File.Exists(dbName)) SQLiteConnection.CreateFile(dbName);

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                string usersTable = @"CREATE TABLE IF NOT EXISTS Users (
                    UserID TEXT PRIMARY KEY,
                    Username TEXT UNIQUE NOT NULL,
                    Password TEXT NOT NULL,
                    Role TEXT,
                    Status TEXT DEFAULT 'Active',
                    CreatedDate TEXT,
                    MustChangePassword INTEGER NOT NULL DEFAULT 0
                );";

                using (var cmd = new SQLiteCommand(usersTable, conn))
                    cmd.ExecuteNonQuery();

                string checkUsersSql = "SELECT COUNT(*) FROM Users";
                using (var cmd = new SQLiteCommand(checkUsersSql, conn))
                {
                    int userCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    if (userCount == 0)
                    {
                        string insertUsersSql = @"INSERT INTO Users (UserID, Username, Password, Role, Status, CreatedDate, MustChangePassword) 
                            VALUES ('USR001', 'admin', '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9', 'Administrator', 'Active', @date, 1)";
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

                string gymProfileTable = @"CREATE TABLE IF NOT EXISTS GymProfile (
                    ID INTEGER PRIMARY KEY CHECK (ID = 1),
                    GymName TEXT,
                    Address TEXT,
                    ContactNumber TEXT,
                    Email TEXT,
                    LogoPath TEXT
                );";

                using (var cmd = new SQLiteCommand(gymProfileTable, conn))
                    cmd.ExecuteNonQuery();
            }

            MigrateUsersTable();
        }

        public static void MigrateUsersTable()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                bool hasMustChange = false;
                using (var cmd = new SQLiteCommand("PRAGMA table_info(Users)", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader["name"]?.ToString() == "MustChangePassword")
                        {
                            hasMustChange = true;
                            break;
                        }
                    }
                }

                if (!hasMustChange)
                {
                    string alter = "ALTER TABLE Users ADD COLUMN MustChangePassword INTEGER NOT NULL DEFAULT 0";
                    using (var cmd = new SQLiteCommand(alter, conn))
                        cmd.ExecuteNonQuery();
                }
            }
        }

        public static void RestoreDefaultProfile()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = @"INSERT OR REPLACE INTO GymProfile (ID, GymName, Address, ContactNumber, Email, LogoPath) 
                       VALUES (1, 'Gym', '', '', '', '')";
                using (var cmd = new SQLiteCommand(sql, conn))
                    cmd.ExecuteNonQuery();
            }
            ProfileUpdated?.Invoke();
        }

        public static void SaveGymProfile(string name, string address, string contact, string email, string logo)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = @"INSERT OR REPLACE INTO GymProfile (ID, GymName, Address, ContactNumber, Email, LogoPath)  
                       VALUES (1, @name, @address, @contact, @email, @logo)";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@address", address);
                    cmd.Parameters.AddWithValue("@contact", contact);
                    cmd.Parameters.AddWithValue("@email", email);
                    cmd.Parameters.AddWithValue("@logo", logo);
                    cmd.ExecuteNonQuery();
                }
            }
            ProfileUpdated?.Invoke();
        }

        public static Dictionary<string, string> GetGymProfile()
        {
            var profile = new Dictionary<string, string>();
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT * FROM GymProfile WHERE ID = 1", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        profile["GymName"] = reader["GymName"]?.ToString() ?? string.Empty;
                        profile["Address"] = reader["Address"]?.ToString() ?? string.Empty;
                        profile["ContactNumber"] = reader["ContactNumber"]?.ToString() ?? string.Empty;
                        profile["Email"] = reader["Email"]?.ToString() ?? string.Empty;
                        profile["LogoPath"] = reader["LogoPath"]?.ToString() ?? string.Empty;
                    }
                }
            }
            return profile;
        }

        public static void MigrateRatesTable()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                bool hasArchived = false;
                using (var cmd = new SQLiteCommand("PRAGMA table_info(Rates)", conn))
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
                // Logic Fix: ExpiryDate is today. This query only expires if Date(ExpiryDate) < today. 
                // This keeps daily members active for the duration of the current calendar date.
                string updateSql = "UPDATE Members SET Status = 'Expired' WHERE ExpiryDate != '-' AND ExpiryDate != '' AND Date(ExpiryDate) < Date('now') AND Status = 'Active'";
                using (var cmd = new SQLiteCommand(updateSql, conn))
                    cmd.ExecuteNonQuery();
            }
        }
    }
}