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

                // 1. Users Table
                string usersTable = @"CREATE TABLE IF NOT EXISTS Users (
            UserID TEXT PRIMARY KEY,
            Username TEXT UNIQUE NOT NULL,
            Password TEXT NOT NULL,
            Role TEXT,
            Status TEXT DEFAULT 'Active',
            CreatedDate TEXT,
            MustChangePassword INTEGER NOT NULL DEFAULT 0
        );";
                using (var cmd = new SQLiteCommand(usersTable, conn)) cmd.ExecuteNonQuery();

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

                // 2. Rates Table
                string ratesTable = @"CREATE TABLE IF NOT EXISTS Rates (
                RateID INTEGER PRIMARY KEY AUTOINCREMENT,
                PlanName TEXT UNIQUE NOT NULL,
                Price REAL NOT NULL,
                DurationDays INTEGER NOT NULL,
                IsArchived INTEGER NOT NULL DEFAULT 0
            );";
                using (var cmd = new SQLiteCommand(ratesTable, conn)) cmd.ExecuteNonQuery();

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
                        using (var insertCmd = new SQLiteCommand(insertRatesSql, conn)) insertCmd.ExecuteNonQuery();
                    }
                }

                // 3. Fixed / Member Type Discounts Table (Student & Senior Tiers)
                string fixedDiscountsTable = @"CREATE TABLE IF NOT EXISTS Discounts (
            DiscountID TEXT PRIMARY KEY,
            TargetType TEXT UNIQUE NOT NULL, -- 'Student' or 'Senior'
            Percentage REAL NOT NULL DEFAULT 0,
            ApplicableRates TEXT NOT NULL DEFAULT 'All' -- Comma-separated or 'All'
        );";
                using (var cmd = new SQLiteCommand(fixedDiscountsTable, conn)) cmd.ExecuteNonQuery();

                // Seed default fixed discounts if empty
                string checkDiscounts = "SELECT COUNT(*) FROM Discounts";
                using (var cmd = new SQLiteCommand(checkDiscounts, conn))
                {
                    if (Convert.ToInt32(cmd.ExecuteScalar() ?? 0) == 0)
                    {
                        string seedDiscounts = @"INSERT INTO Discounts (DiscountID, TargetType, Percentage, ApplicableRates) VALUES 
                    ('DSC001', 'Student', 10.0, 'All'),
                    ('DSC002', 'Senior', 20.0, 'All')";
                        using (var seedCmd = new SQLiteCommand(seedDiscounts, conn)) seedCmd.ExecuteNonQuery();
                    }
                }

                // 4. Promotional Discounts Table
                string promosTable = @"CREATE TABLE IF NOT EXISTS Promos (
            PromoID INTEGER PRIMARY KEY AUTOINCREMENT,
            PromoCode TEXT UNIQUE NOT NULL,
            PromoName TEXT NOT NULL,
            Percentage REAL NOT NULL DEFAULT 0,
            StartDate TEXT NOT NULL,
            EndDate TEXT NOT NULL,
            ApplicableRates TEXT NOT NULL DEFAULT 'All'
        );";
                using (var cmd = new SQLiteCommand(promosTable, conn)) cmd.ExecuteNonQuery();

                // 5. Members Table
                string memberTable = @"CREATE TABLE IF NOT EXISTS Members (
            MemberID TEXT PRIMARY KEY,
            FirstName TEXT,
            MiddleInitial TEXT,
            LastName TEXT,
            FullName TEXT,
            Phone TEXT,
            Gender TEXT,
            Birthday TEXT,
            MemberType TEXT,
            DateJoined TEXT,
            ExpiryDate TEXT,
            Status TEXT,
            PhotoPath TEXT
        );";
                using (var cmd = new SQLiteCommand(memberTable, conn)) cmd.ExecuteNonQuery();

                // 6. Payments Table (UPDATED: Added DiscountAmount column text log entry)
                string paymentTable = @"CREATE TABLE IF NOT EXISTS Payments (
            PaymentID INTEGER PRIMARY KEY AUTOINCREMENT,
            MemberID TEXT,
            MemberName TEXT,
            AmountPaid REAL,
            TotalAmount REAL,
            DiscountAmount REAL DEFAULT 0,
            Change REAL,
            PaymentMode TEXT,
            MembershipType TEXT,
            DateOfTransaction TEXT,
            NewExpiryDate TEXT,
            FOREIGN KEY(MemberID) REFERENCES Members(MemberID)
        );";
                using (var cmd = new SQLiteCommand(paymentTable, conn)) cmd.ExecuteNonQuery();

                // 7. Attendance Table
                string attendanceTable = @"CREATE TABLE IF NOT EXISTS Attendance (
            AttendanceID INTEGER PRIMARY KEY AUTOINCREMENT,
            MemberID TEXT,
            CheckInTime TEXT,
            CheckInDate TEXT,
            CheckOutTime TEXT,
            FOREIGN KEY(MemberID) REFERENCES Members(MemberID)
        );";
                using (var cmd = new SQLiteCommand(attendanceTable, conn)) cmd.ExecuteNonQuery();

                // 8. GymProfile Table
                string gymProfileTable = @"CREATE TABLE IF NOT EXISTS GymProfile (
            ID INTEGER PRIMARY KEY CHECK (ID = 1),
            GymName TEXT,
            Address TEXT,
            ContactNumber TEXT,
            Email TEXT,
            LogoPath TEXT
        );";
                using (var cmd = new SQLiteCommand(gymProfileTable, conn)) cmd.ExecuteNonQuery();
            }

            MigrateUsersTable();
            MigratePaymentsTableForDiscounts();
        }

        // FIXED CS8619: Enforced clear non-nullable string parsing rules
        public static (double percentage, string allowedRates) GetFixedDiscountConfig(string memberType)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = "SELECT Percentage, ApplicableRates FROM Discounts WHERE LOWER(TargetType) = LOWER(@type)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@type", memberType);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            double percentage = Convert.ToDouble(reader["Percentage"]);
                            string allowedRates = reader["ApplicableRates"]?.ToString() ?? "All";
                            return (percentage, allowedRates);
                        }
                    }
                }
            }
            return (0, "All");
        }

        public static (double percentage, string allowedRates) GetPromoConfig(string code)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = @"SELECT Percentage, ApplicableRates FROM Promos 
                       WHERE UPPER(PromoCode) = UPPER(@code) 
                       AND Date('now') BETWEEN Date(StartDate) AND Date(EndDate)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@code", code);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            double percentage = Convert.ToDouble(reader["Percentage"]);
                            string allowedRates = reader["ApplicableRates"]?.ToString() ?? "All";
                            return (percentage, allowedRates);
                        }
                    }
                }
            }
            return (0, string.Empty);
        }

        private static void MigratePaymentsTableForDiscounts()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                bool hasDiscountCol = false;
                using (var cmd = new SQLiteCommand("PRAGMA table_info(Payments)", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader["name"]?.ToString() == "DiscountAmount")
                        {
                            hasDiscountCol = true;
                            break;
                        }
                    }
                }
                if (!hasDiscountCol)
                {
                    string alter = "ALTER TABLE Payments ADD COLUMN DiscountAmount REAL DEFAULT 0";
                    using (var cmd = new SQLiteCommand(alter, conn)) cmd.ExecuteNonQuery();
                }
            }
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

        public static void MigrateDiscountsTable()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                bool hasArchived = false;
                using (var cmd = new SQLiteCommand("PRAGMA table_info(Discounts)", conn))
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
                    string alter = "ALTER TABLE Discounts ADD COLUMN IsArchived INTEGER NOT NULL DEFAULT 0";
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

        public static bool IsPhoneNumberDuplicate(string phone, string excludeMemberId = "")
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = "SELECT COUNT(*) FROM Members WHERE Phone = @phone AND MemberID != @excludeId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@phone", phone);
                    cmd.Parameters.AddWithValue("@excludeId", excludeMemberId);
                    return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
                }
            }
        }

        public static bool IsNameCombinationDuplicate(string firstName, string mi, string lastName, string excludeMemberId = "")
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = @"SELECT COUNT(*) FROM Members 
                       WHERE LOWER(FirstName) = LOWER(@fName) 
                       AND LOWER(MiddleInitial) = LOWER(@mi) 
                       AND LOWER(LastName) = LOWER(@lName) 
                       AND MemberID != @excludeId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@fName", firstName);
                    cmd.Parameters.AddWithValue("@mi", mi);
                    cmd.Parameters.AddWithValue("@lName", lastName);
                    cmd.Parameters.AddWithValue("@excludeId", excludeMemberId);
                    return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
                }
            }
        }

        public static void RefreshMemberStatuses()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string updateSql = "UPDATE Members SET Status = 'Expired' WHERE ExpiryDate != '-' AND ExpiryDate != '' AND Date(ExpiryDate) < Date('now') AND Status = 'Active'";
                using (var cmd = new SQLiteCommand(updateSql, conn))
                    cmd.ExecuteNonQuery();
            }
        }
    }
}