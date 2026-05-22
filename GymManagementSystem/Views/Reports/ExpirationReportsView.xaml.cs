using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using GymManagementSystem.Models;
using GymManagementSystem.Views.MainViews;
using GymManagementSystem.Views.Windows;
using Microsoft.Win32;

namespace GymManagementSystem.Views.Reports
{
    public partial class ExpirationReportsView : UserControl
    {
        private const int ExpiringWindowDays = 7;
        private const int CriticalWindowDays = 3;
        private const double ProgressBarMaxWidth = 100.0;

        private List<ExpirationRecord> _allExpirationRecords = new();
        public ObservableCollection<ExpirationRecord> FilteredExpirationRecords { get; set; } = new();

        public ExpirationReportsView()
        {
            InitializeComponent();
            dgExpirations.ItemsSource = FilteredExpirationRecords;
            LoadExpirationsData();
        }

        private void TxtExpirationSearch_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyExpirationFilter();

        private void LoadExpirationsData()
        {
            _allExpirationRecords.Clear();
            FilteredExpirationRecords.Clear();

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string sql = $@"SELECT M.MemberID, M.FullName, M.Phone, M.ExpiryDate, M.Status,
                         COALESCE((SELECT P.MembershipType FROM Payments P 
                                   WHERE P.MemberID = M.MemberID
                                     AND IFNULL(P.PaymentMode, '') <> 'Refund'
                                     AND IFNULL(P.PaymentMode, '') <> 'Refunded'
                                     AND IFNULL(P.MembershipType, '') NOT LIKE '[REFUND]%'
                                     AND IFNULL(P.MembershipType, '') NOT LIKE '[REFUNDED]%'
                                   ORDER BY P.PaymentID DESC LIMIT 1), '-') as PlanName
                         FROM Members M
                         WHERE Date(M.ExpiryDate) BETWEEN Date('{today}') AND Date('{today}', '+{ExpiringWindowDays} days')
                         AND M.Status = 'Active'
                         ORDER BY Date(M.ExpiryDate) ASC";

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string expiryStr = reader["ExpiryDate"]?.ToString() ?? string.Empty;
                                int daysRemaining = ExpirationUiHelper.CalcDaysRemaining(expiryStr);
                                string urgency = ExpirationUiHelper.ClassifyUrgency(daysRemaining);
                                double progress = daysRemaining <= 0
                                    ? 0
                                    : Math.Min((double)daysRemaining / ExpiringWindowDays, 1.0) * ProgressBarMaxWidth;

                                _allExpirationRecords.Add(new ExpirationRecord
                                {
                                    MemberID = reader["MemberID"]?.ToString() ?? string.Empty,
                                    FullName = reader["FullName"]?.ToString() ?? string.Empty,
                                    Phone = reader["Phone"]?.ToString() ?? string.Empty,
                                    PlanType = reader["PlanName"]?.ToString() ?? "-",
                                    ExpiryDate = ExpirationUiHelper.FormatExpiryDate(expiryStr),
                                    DaysRemaining = daysRemaining,
                                    DaysRemainingLabel = ExpirationUiHelper.BuildDaysLabel(daysRemaining),
                                    UrgencyLevel = urgency,
                                    ProgressWidth = progress
                                });
                            }
                        }
                    }

                    using (var pendingCmd = new SQLiteCommand(
                        "SELECT COUNT(*) FROM Members WHERE Status = 'Expired'", conn))
                    {
                        lblPendingRenewals.Text = Convert.ToInt32(pendingCmd.ExecuteScalar() ?? 0).ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading expirations data: " + ex.Message);
                return;
            }

            ApplyNotificationLabels(_allExpirationRecords);
            ApplyExpirationFilter();
        }

        private void ApplyNotificationLabels(IEnumerable<ExpirationRecord> records)
        {
            var ids = records.Select(r => r.MemberID).ToList();
            var notifiedDates = NotificationHelper.GetLatestNotifiedDates(ids);

            foreach (var record in records)
            {
                notifiedDates.TryGetValue(record.MemberID, out string? lastDate);
                record.LastNotifiedDate = lastDate;
                record.LastNotifiedLabel = BuildLastNotifiedColumnLabel(lastDate);
            }
        }

        private static string BuildLastNotifiedColumnLabel(string? lastNotifiedDate)
        {
            if (NotificationHelper.WasNotifiedOnDate(lastNotifiedDate, NotificationHelper.Today))
                return "Today";
            if (NotificationHelper.WasNotifiedOnDate(lastNotifiedDate, NotificationHelper.Yesterday))
                return "Yesterday";
            if (!string.IsNullOrEmpty(lastNotifiedDate)
                && DateTime.TryParse(lastNotifiedDate, out DateTime date))
            {
                return date.ToString("yyyy-MM-dd");
            }

            return "Never";
        }

        private void ApplyExpirationFilter()
        {
            string query = txtExpirationSearch.Text.Trim().ToLower();
            FilteredExpirationRecords.Clear();

            int critical = 0;

            foreach (var r in _allExpirationRecords)
            {
                if (string.IsNullOrEmpty(query) || r.FullName.ToLower().Contains(query))
                {
                    FilteredExpirationRecords.Add(r);
                    if (r.DaysRemaining >= 0 && r.DaysRemaining <= CriticalWindowDays)
                        critical++;
                }
            }

            lblCriticalCount.Text = critical.ToString();
        }

        private List<NotificationMemberItem> BuildCriticalNotificationItems()
        {
            var criticalRecords = _allExpirationRecords
                .Where(r => r.DaysRemaining >= 0 && r.DaysRemaining <= CriticalWindowDays)
                .ToList();

            ApplyNotificationLabels(criticalRecords);

            return criticalRecords.Select(r => new NotificationMemberItem
            {
                MemberID = r.MemberID,
                FullName = r.FullName,
                Phone = r.Phone,
                ExpiryDate = r.ExpiryDate,
                DaysRemaining = r.DaysRemaining,
                DaysRemainingLabel = r.DaysRemainingLabel,
                UrgencyLevel = r.UrgencyLevel,
                LastNotifiedDate = r.LastNotifiedDate,
                NotifiedStatusLabel = NotificationHelper.BuildNotifiedStatusLabel(r.LastNotifiedDate),
                IsSelected = true
            }).ToList();
        }

        private void NotifyCritical_Click(object sender, RoutedEventArgs e)
        {
            var items = BuildCriticalNotificationItems();
            if (items.Count == 0)
            {
                MessageBox.Show("There are no critical members to notify within the next 3 days.",
                    "Review Notifications", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenReviewDialog(items);
        }

        private void OpenReviewDialog(List<NotificationMemberItem> items)
        {
            var dialog = new ReviewNotificationsWindow(items)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                MessageBox.Show($"Successfully notified {dialog.NotifiedCount} members.",
                    "Notifications Sent", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadExpirationsData();
            }
        }

        private void PayMember_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is ExpirationRecord record)
                PaymentNavigationHelper.TryNavigateToPayment(this, record.MemberID);
        }

        private void ViewExpiredMembers_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow == null)
                return;

            var membersView = new MembersView();
            membersView.SetStatusFilter("Expired");
            mainWindow.MainFrame.Content = membersView;
            mainWindow.btnNavMembers.IsChecked = true;
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (FilteredExpirationRecords.Count == 0)
            {
                MessageBox.Show("There are no expiration records to export.",
                    "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Expiration Report",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"expiration-report-{DateTime.Now:yyyyMMdd}.csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                using (var writer = new StreamWriter(dialog.FileName))
                {
                    writer.WriteLine("Member ID,Member Name,Phone,Plan Type,Expiry Date,Days Remaining,Last Notified");
                    foreach (var record in FilteredExpirationRecords)
                    {
                        writer.WriteLine(string.Join(",",
                            Csv(record.MemberID),
                            Csv(record.FullName),
                            Csv(record.Phone),
                            Csv(record.PlanType),
                            Csv(record.ExpiryDate),
                            Csv(record.DaysRemainingLabel),
                            Csv(record.LastNotifiedLabel)));
                    }
                }

                MessageBox.Show("Expiration report exported successfully.",
                    "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exporting expiration report: " + ex.Message,
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
