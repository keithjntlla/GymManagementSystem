using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Data.SQLite;
using GymManagementSystem.Views.MainViews;

namespace GymManagementSystem.Views.MainViews
{
    public class ExpiringMember
    {
        public string FullName { get; set; } = "";
        public string MemberID { get; set; } = "";
        public string ExpiryDate { get; set; } = "";
        public string DaysLeft { get; set; } = "";
    }

    public class RecentTransaction
    {
        public string MemberName { get; set; } = "";
        public string MembershipType { get; set; } = "";
        public string AmountPaid { get; set; } = "";
        public string Date { get; set; } = "";
        public string PaymentMode { get; set; } = "";
    }

    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
            DatabaseHelper.ProfileUpdated += RefreshHeader; // Subscribe[cite: 19]
            RefreshHeader();
            LoadDashboardData();
        }

        private void RefreshHeader()
        {
            var profile = DatabaseHelper.GetGymProfile();
            string gymName = profile.GetValueOrDefault("GymName", "Gym");
            txtWelcomeMessage.Text = $"Welcome to {gymName}! Here's what's happening today.";
        }

        private void LoadDashboardData()
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // Total active members
                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM Members WHERE Status = 'Active'", conn))
                        lblTotalMembers.Text = Convert.ToInt32(cmd.ExecuteScalar() ?? 0).ToString();

                    // Expired members
                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM Members WHERE Status = 'Expired'", conn))
                        lblExpiredMembers.Text = Convert.ToInt32(cmd.ExecuteScalar() ?? 0).ToString();

                    // Today's check-ins
                    using (var cmd = new SQLiteCommand(
                        "SELECT COUNT(*) FROM Attendance WHERE CheckInDate = @today", conn))
                    {
                        cmd.Parameters.AddWithValue("@today", DateTime.Now.ToString("yyyy-MM-dd"));
                        lblTodayCheckIns.Text = Convert.ToInt32(cmd.ExecuteScalar() ?? 0).ToString();
                    }

                    // Currently checked in (checked in today, no checkout)
                    using (var cmd = new SQLiteCommand(
                        "SELECT COUNT(*) FROM Attendance WHERE CheckInDate = @today AND (CheckOutTime IS NULL OR CheckOutTime = '')", conn))
                    {
                        cmd.Parameters.AddWithValue("@today", DateTime.Now.ToString("yyyy-MM-dd"));
                        lblCurrentlyIn.Text = Convert.ToInt32(cmd.ExecuteScalar() ?? 0).ToString();
                    }

                    // Today's revenue
                    using (var cmd = new SQLiteCommand(
                        "SELECT SUM(AmountPaid) FROM Payments WHERE DateOfTransaction = @today", conn))
                    {
                        cmd.Parameters.AddWithValue("@today", DateTime.Now.ToString("yyyy-MM-dd"));
                        object result = cmd.ExecuteScalar();
                        double revenue = result != null && result != DBNull.Value ? Convert.ToDouble(result) : 0;
                        lblTodayRevenue.Text = $"₱{revenue:N2}";
                    }

                    // Monthly revenue
                    using (var cmd = new SQLiteCommand(
                        "SELECT SUM(AmountPaid) FROM Payments WHERE strftime('%Y-%m', DateOfTransaction) = @month", conn))
                    {
                        cmd.Parameters.AddWithValue("@month", DateTime.Now.ToString("yyyy-MM"));
                        object result = cmd.ExecuteScalar();
                        double revenue = result != null && result != DBNull.Value ? Convert.ToDouble(result) : 0;
                        lblMonthlyRevenue.Text = $"₱{revenue:N2}";
                    }

                    // New members this month
                    using (var cmd = new SQLiteCommand(
                        "SELECT COUNT(*) FROM Members WHERE strftime('%Y-%m', DateJoined) = @month", conn))
                    {
                        cmd.Parameters.AddWithValue("@month", DateTime.Now.ToString("yyyy-MM"));
                        lblNewMembers.Text = Convert.ToInt32(cmd.ExecuteScalar() ?? 0).ToString();
                    }

                    // Expiring in 3 days count
                    using (var cmd = new SQLiteCommand(@"SELECT COUNT(*) FROM Members 
                        WHERE Status = 'Active' AND ExpiryDate != '-' 
                        AND DATE(ExpiryDate) BETWEEN DATE('now') AND DATE('now', '+3 days')", conn))
                        lblExpiringSubscriptions.Text = Convert.ToInt32(cmd.ExecuteScalar() ?? 0).ToString();

                    // Expiring soon list (names)
                    var expiring = new List<ExpiringMember>();
                    using (var cmd = new SQLiteCommand(@"SELECT MemberID, FullName, ExpiryDate,
                        CAST(JULIANDAY(ExpiryDate) - JULIANDAY('now') AS INTEGER) AS DaysLeft
                        FROM Members WHERE Status = 'Active' AND ExpiryDate != '-'
                        AND DATE(ExpiryDate) BETWEEN DATE('now') AND DATE('now', '+3 days')
                        ORDER BY ExpiryDate ASC", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int days = Convert.ToInt32(reader["DaysLeft"]);
                            expiring.Add(new ExpiringMember
                            {
                                MemberID = reader["MemberID"]?.ToString() ?? "",
                                FullName = reader["FullName"]?.ToString() ?? "",
                                ExpiryDate = reader["ExpiryDate"]?.ToString() ?? "",
                                DaysLeft = days == 0 ? "Today" : $"{days}d left"
                            });
                        }
                    }
                    dgExpiringSoon.ItemsSource = expiring;
                    ExpiringSoonSection.Visibility = Visibility.Visible;

                    // Recent transactions (last 5)
                    var transactions = new List<RecentTransaction>();
                    using (var cmd = new SQLiteCommand(@"SELECT MemberName, MembershipType, AmountPaid, DateOfTransaction, PaymentMode
                        FROM Payments ORDER BY PaymentID DESC LIMIT 5", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            transactions.Add(new RecentTransaction
                            {
                                MemberName = reader["MemberName"]?.ToString() ?? "",
                                MembershipType = reader["MembershipType"]?.ToString() ?? "",
                                AmountPaid = $"₱{Convert.ToDouble(reader["AmountPaid"]):N2}",
                                Date = reader["DateOfTransaction"]?.ToString() ?? "",
                                PaymentMode = reader["PaymentMode"]?.ToString() ?? ""
                            });
                        }
                    }
                    dgRecentTransactions.ItemsSource = transactions;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading dashboard data: " + ex.Message);
            }
        }
    }
}