using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Data.SQLite;

namespace GymManagementSystem
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
            LoadDashboardData();
        }

        private void LoadDashboardData()
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // Get total active members
                    string activeMembersSql = "SELECT COUNT(*) FROM Members WHERE Status = 'Active'";
                    using (var cmd = new SQLiteCommand(activeMembersSql, conn))
                    {
                        int activeMembers = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                        lblTotalMembers.Text = activeMembers.ToString();
                    }

                    // Get today's revenue
                    string revenueSql = "SELECT SUM(AmountPaid) FROM Payments WHERE DateOfTransaction = @today";
                    using (var cmd = new SQLiteCommand(revenueSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@today", DateTime.Now.ToString("yyyy-MM-dd"));
                        object result = cmd.ExecuteScalar();
                        double revenue = result != null && result != DBNull.Value ? Convert.ToDouble(result) : 0;
                        lblTodayRevenue.Text = $"₱{revenue:N2}";
                    }

                    // Get expiring subscriptions (within 3 days)
                    string expiringSql = @"SELECT COUNT(*) FROM Members 
                                          WHERE Status = 'Active' 
                                          AND ExpiryDate != '-' 
                                          AND DATE(ExpiryDate) BETWEEN DATE('now') AND DATE('now', '+3 days')";
                    using (var cmd = new SQLiteCommand(expiringSql, conn))
                    {
                        int expiringCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                        lblExpiringSubscriptions.Text = expiringCount.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading dashboard data: " + ex.Message);
            }
        }
    }
}
