using GymManagementSystem.Views.MainViews;
using GymManagementSystem.Views.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using GymManagementSystem.Models; // Reusing the global data model library path

namespace GymManagementSystem.Views.MainViews
{
    public class ExpiringMember
    {
        public string FullName { get; set; } = "";
        public string MemberID { get; set; } = "";
        public string ExpiryDate { get; set; } = "";
        public string DaysLeft { get; set; } = "";
    }

    public partial class HomeView : UserControl
    {
        // REUSED MODEL: Swapped collection target array item type to use PaymentRecord
        public ObservableCollection<PaymentRecord> RecentTransactionsList { get; set; }
            = new ObservableCollection<PaymentRecord>();

        public HomeView()
        {
            InitializeComponent();
            DatabaseHelper.ProfileUpdated += RefreshHeader;
            RefreshHeader();
            LoadDashboardData();
        }

        private void RefreshHeader()
        {
            string username = LoginWindow.CurrentUser?.Username ?? "User";
            txtWelcomeMessage.Text = $"Welcome back, {username}! Here's what's happening today.";
        }

        private void LoadDashboardData()
        {
            RecentTransactionsList.Clear();
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

                    // Today's revenue (TotalAmount)
                    using (var cmd = new SQLiteCommand(
                        "SELECT SUM(TotalAmount) FROM Payments WHERE DateOfTransaction = @today", conn))
                    {
                        cmd.Parameters.AddWithValue("@today", DateTime.Now.ToString("yyyy-MM-dd"));
                        object result = cmd.ExecuteScalar();
                        double revenue = result != null && result != DBNull.Value ? Convert.ToDouble(result) : 0;
                        lblTodayRevenue.Text = $"₱{revenue:N2}";
                    }

                    // Monthly revenue (TotalAmount)
                    using (var cmd = new SQLiteCommand(
                        "SELECT SUM(TotalAmount) FROM Payments WHERE strftime('%Y-%m', DateOfTransaction) = @month", conn))
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
                    using (var cmd = new SQLiteCommand(@"SELECT MemberName, MembershipType, TotalAmount, AmountPaid, DateOfTransaction, PaymentMode
                        FROM Payments ORDER BY PaymentID DESC LIMIT 5", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Map fallback calculations to safely handle older database rows
                            double netCost = reader["TotalAmount"] != DBNull.Value
                                ? Convert.ToDouble(reader["TotalAmount"])
                                : Convert.ToDouble(reader["AmountPaid"] ?? 0);

                            RecentTransactionsList.Add(new PaymentRecord
                            {
                                MemberName = reader["MemberName"]?.ToString() ?? "",
                                MembershipType = reader["MembershipType"]?.ToString() ?? "",
                                TotalAmount = netCost,
                                DateOfTransaction = reader["DateOfTransaction"]?.ToString() ?? "",
                                PaymentMode = reader["PaymentMode"]?.ToString() ?? ""
                            });
                        }
                    }
                    dgRecentTransactions.ItemsSource = RecentTransactionsList;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading dashboard data: " + ex.Message);
            }
        }
    }
}