using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using GymManagementSystem.Models;
using GymManagementSystem.Views.Reports;

namespace GymManagementSystem.Views.Reports
{
    public partial class ExpirationReportsView : UserControl
    {
        private List<ExpirationRecord> _allExpirationRecords = new List<ExpirationRecord>();
        public ObservableCollection<ExpirationRecord> FilteredExpirationRecords { get; set; } = new ObservableCollection<ExpirationRecord>();

        private const double ProgressBarMaxWidth = 100.0;
        public ExpirationReportsView()
        {
            InitializeComponent();
            dgExpirations.ItemsSource = FilteredExpirationRecords;
            LoadExpirationsData();
        }

        private void CbExpirationWindow_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && ExpirationsReportGrid.Visibility == Visibility.Visible)
                LoadExpirationsData();
        }

        private void TxtExpirationSearch_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyExpirationFilter();

        private void LoadExpirationsData()
        {
            string window = (cbExpirationWindow.SelectedItem as ComboBoxItem)?.Content.ToString()
                            ?? "Expiring Today";

            _allExpirationRecords.Clear();
            FilteredExpirationRecords.Clear();

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string sql;

            // Base query with a subquery to get the latest plan name from the Payments table
            string baseQuery = @"SELECT M.MemberID, M.FullName, M.Phone, M.ExpiryDate, M.Status,
                         COALESCE((SELECT P.MembershipType FROM Payments P 
                                   WHERE P.MemberID = M.MemberID 
                                   ORDER BY P.PaymentID DESC LIMIT 1), '-') as PlanName
                         FROM Members M";

            if (window == "Expired (Past 30 Days)")
            {
                string past30 = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
                sql = $@"{baseQuery}
                 WHERE  Date(M.ExpiryDate) BETWEEN Date('{past30}') AND Date('{today}', '-1 day')
                 AND    M.Status = 'Expired'
                 ORDER BY Date(M.ExpiryDate) ASC";
            }
            else
            {
                int days = window switch
                {
                    "Expiring Today" => 0,
                    "Expiring within 3 Days" => 3,
                    "Expiring within 7 Days" => 7,
                    _ => 7
                };

                sql = $@"{baseQuery}
                 WHERE  Date(M.ExpiryDate) BETWEEN Date('{today}') AND Date('{today}', '+{days} days')
                 AND    M.Status = 'Active'
                 ORDER BY Date(M.ExpiryDate) ASC";
            }

            int maxDays = window switch
            {
                "Expiring Today" => 1,
                "Expiring within 3 Days" => 3,
                "Expiring within 7 Days" => 7,
                _ => 30
            };

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string expiryStr = reader["ExpiryDate"]?.ToString() ?? string.Empty;
                            int daysRemaining = CalcDaysRemaining(expiryStr);
                            string urgency = ClassifyUrgency(daysRemaining);
                            double progress = daysRemaining <= 0
                                ? 0
                                : Math.Min((double)daysRemaining / maxDays, 1.0) * ProgressBarMaxWidth;

                            _allExpirationRecords.Add(new ExpirationRecord
                            {
                                MemberID = reader["MemberID"]?.ToString() ?? string.Empty,
                                FullName = reader["FullName"]?.ToString() ?? string.Empty,
                                Phone = reader["Phone"]?.ToString() ?? string.Empty,
                                // Use the real plan name from the database subquery
                                PlanType = reader["PlanName"]?.ToString() ?? "-",
                                ExpiryDate = FormatExpiryDate(expiryStr),
                                DaysRemaining = daysRemaining,
                                DaysRemainingLabel = BuildDaysLabel(daysRemaining),
                                UrgencyLevel = urgency,
                                ProgressWidth = progress
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading expirations data: " + ex.Message);
                return;
            }

            ApplyExpirationFilter();
        }

        private void ApplyExpirationFilter()
        {
            string query = txtExpirationSearch.Text.Trim().ToLower();
            FilteredExpirationRecords.Clear();

            int critical = 0;
            int pending = 0;

            foreach (var r in _allExpirationRecords)
            {
                if (string.IsNullOrEmpty(query) || r.FullName.ToLower().Contains(query))
                {
                    FilteredExpirationRecords.Add(r);
                    if (r.UrgencyLevel == "Critical") critical++;
                    if (r.UrgencyLevel == "Expired") pending++;
                }
            }

            lblCriticalCount.Text = critical.ToString();
            lblPendingRenewals.Text = pending.ToString();
        }

        // ── Helpers ─────────────────────────────────────────────────

        private static int CalcDaysRemaining(string expiryDateStr)
        {
            if (DateTime.TryParse(expiryDateStr, out DateTime expiry))
                return (int)(expiry.Date - DateTime.Now.Date).TotalDays;
            return 0;
        }

        private static string ClassifyUrgency(int days)
        {
            if (days < 0) return "Expired";
            if (days <= 2) return "Critical";
            return "Warning";
        }

        private static string BuildDaysLabel(int days)
        {
            if (days < 0) return $"{Math.Abs(days)} days ago";
            if (days == 0) return "0 days";
            return $"{days} days";
        }

        private static string FormatExpiryDate(string raw)
        {
            if (DateTime.TryParse(raw, out DateTime dt))
                return dt.ToString("yyyy-MM-dd");
            return raw;
        }

        // Still used by Expirations tab to show plan badge color
        private static string DeriveMembershipType(string expiryDateString)
        {
            if (DateTime.TryParse(expiryDateString, out DateTime expiry))
            {
                TimeSpan remaining = expiry - DateTime.Now;
                if (remaining.TotalDays <= 1) return "Daily";
                if (remaining.TotalDays <= 7) return "Weekly";
                if (remaining.TotalDays <= 15) return "Half-Month";
                if (remaining.TotalDays <= 30) return "Monthly";
                if (remaining.TotalDays <= 365) return "Yearly";
            }
            return "Unknown";
        }
    }
}