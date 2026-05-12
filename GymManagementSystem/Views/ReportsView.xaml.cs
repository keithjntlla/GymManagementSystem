using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace GymManagementSystem
{
    public partial class ReportsView : UserControl
    {
        // ── Financial ────────────────────────────────────────────────
        public ObservableCollection<PaymentRecord> Transactions { get; set; }
            = new ObservableCollection<PaymentRecord>();

        // ── Attendance ───────────────────────────────────────────────
        private ObservableCollection<AttendanceRecord> _allAttendanceRecords
            = new ObservableCollection<AttendanceRecord>();
        public ObservableCollection<AttendanceRecord> FilteredAttendanceRecords { get; set; }
            = new ObservableCollection<AttendanceRecord>();

        // ── Expirations ──────────────────────────────────────────────
        private List<ExpirationRecord> _allExpirationRecords = new List<ExpirationRecord>();
        public ObservableCollection<ExpirationRecord> FilteredExpirationRecords { get; set; }
            = new ObservableCollection<ExpirationRecord>();

        private const double ProgressBarMaxWidth = 100.0;

        // ────────────────────────────────────────────────────────────
        public ReportsView()
        {
            InitializeComponent();

            dpStart.SelectedDate = DateTime.Now.AddMonths(-1);
            dpEnd.SelectedDate = DateTime.Now;
            dpAttendanceDate.SelectedDate = DateTime.Now;

            dgTransactions.ItemsSource = Transactions;
            dgAttendanceReport.ItemsSource = FilteredAttendanceRecords;
            dgExpirations.ItemsSource = FilteredExpirationRecords;

            LoadPlanFilterComboBox();
            LoadFinancialData();
        }

        // ══════════════════════════════════════════════════════════════
        //  PLAN FILTER COMBOBOX — loaded dynamically from Rates table
        // ══════════════════════════════════════════════════════════════
        private void LoadPlanFilterComboBox()
        {
            // Keep only the first item "All Plans" then add from DB
            while (cbPlanFilter.Items.Count > 1)
                cbPlanFilter.Items.RemoveAt(1);

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT PlanName FROM Rates ORDER BY DurationDays ASC";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cbPlanFilter.Items.Add(new ComboBoxItem
                            {
                                Content = reader["PlanName"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading plan filter: " + ex.Message);
            }

            cbPlanFilter.SelectedIndex = 0;
        }

        // ══════════════════════════════════════════════════════════════
        //  TAB SWITCHING
        // ══════════════════════════════════════════════════════════════
        private void TabFinancial_Click(object sender, RoutedEventArgs e)
        {
            FinancialReportGrid.Visibility = Visibility.Visible;
            AttendanceReportGrid.Visibility = Visibility.Collapsed;
            ExpirationsReportGrid.Visibility = Visibility.Collapsed;
        }

        private void TabAttendance_Click(object sender, RoutedEventArgs e)
        {
            FinancialReportGrid.Visibility = Visibility.Collapsed;
            AttendanceReportGrid.Visibility = Visibility.Visible;
            ExpirationsReportGrid.Visibility = Visibility.Collapsed;
            LoadAttendanceData();
        }

        private void TabExpirations_Click(object sender, RoutedEventArgs e)
        {
            FinancialReportGrid.Visibility = Visibility.Collapsed;
            AttendanceReportGrid.Visibility = Visibility.Collapsed;
            ExpirationsReportGrid.Visibility = Visibility.Visible;
            LoadExpirationsData();
        }

        // ══════════════════════════════════════════════════════════════
        //  FINANCIAL
        // ══════════════════════════════════════════════════════════════
        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) LoadFinancialData();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) LoadFinancialData();
        }

        private void LoadFinancialData()
        {
            if (dpStart.SelectedDate == null || dpEnd.SelectedDate == null) return;

            string startDate = dpStart.SelectedDate.Value.ToString("yyyy-MM-dd");
            string endDate = dpEnd.SelectedDate.Value.ToString("yyyy-MM-dd");
            string planFilter = (cbPlanFilter.SelectedItem as ComboBoxItem)?.Content.ToString()
                                ?? "All Plans";

            Transactions.Clear();
            double totalRevenue = 0;
            double walkInRevenue = 0;
            int walkInCount = 0;
            double subscriptionRevenue = 0;
            int subscriptionCount = 0;

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    var sb = new StringBuilder(
                        "SELECT * FROM Payments WHERE DateOfTransaction BETWEEN @start AND @end");

                    if (planFilter != "All Plans")
                        sb.Append(" AND MembershipType = @plan");

                    sb.Append(" ORDER BY DateOfTransaction DESC");

                    using (var cmd = new SQLiteCommand(sb.ToString(), conn))
                    {
                        cmd.Parameters.AddWithValue("@start", startDate);
                        cmd.Parameters.AddWithValue("@end", endDate);
                        if (planFilter != "All Plans")
                            cmd.Parameters.AddWithValue("@plan", planFilter);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var record = new PaymentRecord
                                {
                                    PaymentID = Convert.ToInt32(reader["PaymentID"]),
                                    MemberID = reader["MemberID"]?.ToString() ?? string.Empty,
                                    MemberName = reader["MemberName"]?.ToString() ?? string.Empty,
                                    AmountPaid = Convert.ToDouble(reader["AmountPaid"]),
                                    PaymentMode = reader["PaymentMode"]?.ToString() ?? string.Empty,
                                    MembershipType = reader["MembershipType"]?.ToString() ?? string.Empty,
                                    DateOfTransaction = reader["DateOfTransaction"]?.ToString() ?? string.Empty
                                };

                                Transactions.Add(record);
                                totalRevenue += record.AmountPaid;

                                if (record.MembershipType == "Daily")
                                { walkInRevenue += record.AmountPaid; walkInCount++; }
                                else
                                { subscriptionRevenue += record.AmountPaid; subscriptionCount++; }
                            }
                        }
                    }
                }

                lblTotalRevenue.Text = $"₱{totalRevenue:N0}";
                lblWalkIns.Text = $"₱{walkInRevenue:N0}";
                lblWalkInsCount.Text = $"{walkInCount} transactions";
                lblSubscriptions.Text = $"₱{subscriptionRevenue:N0}";
                lblSubscriptionsCount.Text = $"{subscriptionCount} transactions";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading financial data: " + ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  ATTENDANCE
        // ══════════════════════════════════════════════════════════════
        private void BtnPrevDay_Click(object sender, RoutedEventArgs e)
            => dpAttendanceDate.SelectedDate =
               (dpAttendanceDate.SelectedDate ?? DateTime.Now).AddDays(-1);

        private void BtnNextDay_Click(object sender, RoutedEventArgs e)
            => dpAttendanceDate.SelectedDate =
               (dpAttendanceDate.SelectedDate ?? DateTime.Now).AddDays(1);

        private void DpAttendanceDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && AttendanceReportGrid.Visibility == Visibility.Visible)
                LoadAttendanceData();
        }

        private void BtnRefreshAttendance_Click(object sender, RoutedEventArgs e)
            => LoadAttendanceData();

        private void TxtAttendanceSearch_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyAttendanceFilter();

        private void ApplyAttendanceFilter()
        {
            string query = txtAttendanceSearch.Text.Trim().ToLower();
            FilteredAttendanceRecords.Clear();

            foreach (var r in _allAttendanceRecords)
            {
                if (string.IsNullOrEmpty(query) ||
                    r.Name.ToLower().Contains(query) ||
                    r.MembershipType.ToLower().Contains(query))
                    FilteredAttendanceRecords.Add(r);
            }

            UpdateAttendanceSummary();
        }

        private void LoadAttendanceData()
        {
            if (dpAttendanceDate.SelectedDate == null) return;

            string selectedDate = dpAttendanceDate.SelectedDate.Value.ToString("yyyy-MM-dd");
            _allAttendanceRecords.Clear();
            FilteredAttendanceRecords.Clear();

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // Join to Payments to get the actual last MembershipType paid for,
                    // instead of guessing from the expiry date
                    string sql = @"
                        SELECT  A.AttendanceID,
                                A.CheckInTime,
                                A.CheckOutTime,
                                M.MemberID,
                                M.FullName,
                                M.Status,
                                COALESCE(
                                    (SELECT P.MembershipType
                                     FROM   Payments P
                                     WHERE  P.MemberID = M.MemberID
                                     ORDER BY P.PaymentID DESC
                                     LIMIT 1),
                                    'Unknown'
                                ) AS MembershipType
                        FROM    Attendance A
                        JOIN    Members M ON A.MemberID = M.MemberID
                        WHERE   A.CheckInDate = @date
                        ORDER BY A.CheckInTime ASC";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@date", selectedDate);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                _allAttendanceRecords.Add(new AttendanceRecord
                                {
                                    AttendanceID = Convert.ToInt32(reader["AttendanceID"] ?? 0),
                                    MemberID = reader["MemberID"]?.ToString() ?? string.Empty,
                                    Name = reader["FullName"]?.ToString() ?? string.Empty,
                                    CheckInTime = FormatTime(reader["CheckInTime"]?.ToString() ?? string.Empty),
                                    CheckOutTime = FormatTime(reader["CheckOutTime"]?.ToString() ?? string.Empty),
                                    MembershipType = reader["MembershipType"]?.ToString() ?? string.Empty,
                                    Status = reader["Status"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading attendance data: " + ex.Message);
                return;
            }

            ApplyAttendanceFilter();
        }

        private void UpdateAttendanceSummary()
        {
            lblTotalVisitors.Text = FilteredAttendanceRecords.Count.ToString();
            lblPeakHour.Text = CalculatePeakHour(_allAttendanceRecords);
        }

        private string CalculatePeakHour(IEnumerable<AttendanceRecord> records)
        {
            var hourCounts = new Dictionary<int, int>();
            foreach (var r in records)
            {
                if (DateTime.TryParse(r.CheckInTime, out DateTime dt))
                {
                    if (!hourCounts.ContainsKey(dt.Hour)) hourCounts[dt.Hour] = 0;
                    hourCounts[dt.Hour]++;
                }
            }
            if (hourCounts.Count == 0) return "--";
            int peak = hourCounts.OrderByDescending(kv => kv.Value).First().Key;
            return DateTime.Today.AddHours(peak).ToString("hh tt");
        }

        // ══════════════════════════════════════════════════════════════
        //  EXPIRATIONS
        // ══════════════════════════════════════════════════════════════
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

            if (window == "Expired (Past 30 Days)")
            {
                string past30 = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
                sql = $@"SELECT MemberID, FullName, Phone, ExpiryDate, Status
                         FROM   Members
                         WHERE  Date(ExpiryDate) BETWEEN Date('{past30}') AND Date('{today}', '-1 day')
                         AND    Status = 'Expired'
                         ORDER BY Date(ExpiryDate) ASC";
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

                sql = $@"SELECT MemberID, FullName, Phone, ExpiryDate, Status
                         FROM   Members
                         WHERE  Date(ExpiryDate) BETWEEN Date('{today}') AND Date('{today}', '+{days} days')
                         AND    Status = 'Active'
                         ORDER BY Date(ExpiryDate) ASC";
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
                            int days = CalcDaysRemaining(expiryStr);
                            string urgency = ClassifyUrgency(days);
                            double progress = days <= 0
                                ? 0
                                : Math.Min((double)days / maxDays, 1.0) * ProgressBarMaxWidth;

                            _allExpirationRecords.Add(new ExpirationRecord
                            {
                                MemberID = reader["MemberID"]?.ToString() ?? string.Empty,
                                FullName = reader["FullName"]?.ToString() ?? string.Empty,
                                Phone = reader["Phone"]?.ToString() ?? string.Empty,
                                PlanType = DeriveMembershipType(expiryStr),
                                ExpiryDate = FormatExpiryDate(expiryStr),
                                DaysRemaining = days,
                                DaysRemainingLabel = BuildDaysLabel(days),
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

        private static string FormatTime(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            if (DateTime.TryParse(raw, out DateTime dt))
                return dt.ToString("hh:mm tt");
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

    // ══════════════════════════════════════════════════════════════════
    //  DATA MODELS
    // ══════════════════════════════════════════════════════════════════
    public class PaymentRecord
    {
        public int PaymentID { get; set; }
        public string MemberID { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public double AmountPaid { get; set; }
        public string PaymentMode { get; set; } = string.Empty;
        public string MembershipType { get; set; } = string.Empty;
        public string DateOfTransaction { get; set; } = string.Empty;
    }

    public class ExpirationRecord
    {
        public string MemberID { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string PlanType { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty;
        public int DaysRemaining { get; set; }
        public string DaysRemainingLabel { get; set; } = string.Empty;
        public string UrgencyLevel { get; set; } = string.Empty;
        public double ProgressWidth { get; set; }
    }
}