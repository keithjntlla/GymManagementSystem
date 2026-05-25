using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
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
using GymManagementSystem.Views.Reports;
using Microsoft.Win32;

namespace GymManagementSystem.Views.Reports
{
    public partial class AttendanceReportsView : UserControl
    {
        private ObservableCollection<AttendanceRecord> _allAttendanceRecords
            = new ObservableCollection<AttendanceRecord>();
        public ObservableCollection<AttendanceRecord> FilteredAttendanceRecords { get; set; }
            = new ObservableCollection<AttendanceRecord>();

        public AttendanceReportsView()
        {
            InitializeComponent();

            dgAttendanceReport.ItemsSource = FilteredAttendanceRecords;
            dpAttendanceDate.SelectedDate = DateTime.Now;
            LoadPlanFilterComboBox();
            LoadAttendanceData();
        }

        private void LoadPlanFilterComboBox()
        {
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
                MessageBox.Show("Error loading attendance plan filter: " + ex.Message);
            }

            cbPlanFilter.SelectedIndex = 0;
        }

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

        private void FilterPlan_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyAttendanceFilter();
        }

        private void ApplyAttendanceFilter()
        {
            string query = txtAttendanceSearch.Text.Trim().ToLower();
            string planFilter = (cbPlanFilter.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All Plans";
            FilteredAttendanceRecords.Clear();

            foreach (var r in _allAttendanceRecords)
            {
                bool searchMatch = string.IsNullOrEmpty(query) ||
                                  r.Name.ToLower().Contains(query) ||
                                  r.MembershipType.ToLower().Contains(query);

                bool planMatch = planFilter == "All Plans" ||
                                 r.MembershipType.Contains(planFilter, StringComparison.OrdinalIgnoreCase);

                if (searchMatch && planMatch)
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
                                       AND   IFNULL(P.PaymentMode, '') <> 'Refund'
                                       AND   IFNULL(P.PaymentMode, '') <> 'Refunded'
                                       AND   IFNULL(P.MembershipType, '') NOT LIKE '[REFUND]%'
                                       AND   IFNULL(P.MembershipType, '') NOT LIKE '[REFUNDED]%'
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

        private static string FormatTime(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            if (DateTime.TryParse(raw, out DateTime dt))
                return dt.ToString("hh:mm tt").ToUpper();
            return raw;
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (FilteredAttendanceRecords.Count == 0)
            {
                MessageBox.Show("There are no attendance records to export.",
                    "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedDate = (dpAttendanceDate.SelectedDate ?? DateTime.Now).ToString("yyyyMMdd");
            var dialog = new SaveFileDialog
            {
                Title = "Export Attendance Report",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"attendance-report-{selectedDate}.csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var gymProfile = DatabaseHelper.GetGymProfile();
                string gymName = gymProfile.ContainsKey("GymName") ? gymProfile["GymName"] : "Gym";
                string gymAddress = gymProfile.ContainsKey("Address") ? gymProfile["Address"] : "";
                string gymContact = gymProfile.ContainsKey("ContactNumber") ? gymProfile["ContactNumber"] : "";
                string gymEmail = gymProfile.ContainsKey("Email") ? gymProfile["Email"] : "";
                string planFilter = (cbPlanFilter.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All Plans";

                using (var writer = new StreamWriter(dialog.FileName))
                {
                    // 1. Gym Profile Block
                    writer.WriteLine("GYM PROFILE");
                    writer.WriteLine($"Gym Name,{Csv(gymName)}");
                    writer.WriteLine($"Address,{Csv(gymAddress)}");
                    writer.WriteLine($"Contact,{Csv(gymContact)}");
                    writer.WriteLine($"Email,{Csv(gymEmail)}");
                    writer.WriteLine();

                    // 2. Report Summary Block
                    writer.WriteLine("REPORT SUMMARY");
                    writer.WriteLine("Report Type,Attendance Report");
                    writer.WriteLine($"Date Generated,{Csv(DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"))}");
                    writer.WriteLine($"Selected Report Date,{Csv((dpAttendanceDate.SelectedDate ?? DateTime.Now).ToString("yyyy-MM-dd"))}");
                    writer.WriteLine($"Plan Filter,{Csv(planFilter)}");
                    writer.WriteLine($"Total Visitors,{Csv(FilteredAttendanceRecords.Count.ToString())}");
                    writer.WriteLine($"Peak Hour,{Csv(lblPeakHour.Text)}");
                    writer.WriteLine();

                    // 3. Report Data Block
                    writer.WriteLine("REPORT DATA");
                    writer.WriteLine("Date,Member ID,Member Name,Check In,Check Out,Membership Plan,Status");
                    foreach (var record in FilteredAttendanceRecords)
                    {
                        writer.WriteLine(string.Join(",",
                            Csv((dpAttendanceDate.SelectedDate ?? DateTime.Now).ToString("yyyy-MM-dd")),
                            Csv(record.MemberID),
                            Csv(record.Name),
                            Csv(record.CheckInTime),
                            Csv(record.CheckOutTime),
                            Csv(record.MembershipType),
                            Csv(record.Status)));
                    }
                }

                MessageBox.Show("Attendance report exported successfully.",
                    "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exporting attendance report: " + ex.Message,
                    "Export CSV", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string Csv(string value)
        {
            string safeValue = value.Replace("\"", "\"\"");
            return $"\"{safeValue}\"";
        }
    }
}
