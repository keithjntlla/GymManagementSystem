using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using GymManagementSystem.Models;

namespace GymManagementSystem.Views.Reports
{
    /// <summary>
    /// Interaction logic for RefundReportsView.xaml
    /// </summary>
    public partial class RefundReportsView : UserControl, INotifyPropertyChanged
    {
        public ObservableCollection<RefundTransaction> RefundTransactions { get; } = new ObservableCollection<RefundTransaction>();

        public int TotalRefundsCount => RefundTransactions.Count;
        public double TotalRefundsAmount { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private DatePicker StartDatePicker => (DatePicker)FindName("dpStart");
        private DatePicker EndDatePicker => (DatePicker)FindName("dpEnd");
        private ComboBox PlanFilterComboBox => (ComboBox)FindName("cbPlanFilter");

        public RefundReportsView()
        {
            InitializeComponent();
            DataContext = this;
            StartDatePicker.SelectedDate = DateTime.Now.AddMonths(-1);
            EndDatePicker.SelectedDate = DateTime.Now;
            LoadPlanFilterComboBox();
            LoadRefundTransactions();
        }

        private void LoadPlanFilterComboBox()
        {
            while (PlanFilterComboBox.Items.Count > 1)
                PlanFilterComboBox.Items.RemoveAt(1);

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
                            PlanFilterComboBox.Items.Add(new ComboBoxItem
                            {
                                Content = reader["PlanName"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading refund plan filter: " + ex.Message);
            }

            PlanFilterComboBox.SelectedIndex = 0;
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) LoadRefundTransactions();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) LoadRefundTransactions();
        }

        private void LoadRefundTransactions()
        {
            if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null) return;

            string startDate = StartDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
            string endDate = EndDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
            string planFilter = (PlanFilterComboBox.SelectedItem as ComboBoxItem)?.Content.ToString()
                                ?? "All Plans";

            RefundTransactions.Clear();
            TotalRefundsAmount = 0;

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    var sql = @"
                        SELECT PaymentID, MemberID, MemberName, TotalAmount, MembershipType, DateOfTransaction,
                               COALESCE(ProcessedBy, 'System') as ProcessedBy
                        FROM Payments
                        WHERE DateOfTransaction BETWEEN @start AND @end
                          AND (
                              IFNULL(PaymentMode, '') = 'Refund'
                              OR IFNULL(MembershipType, '') LIKE '[REFUND]%'
                              OR IFNULL(TotalAmount, 0) < 0
                          )";

                    if (planFilter != "All Plans")
                        sql += " AND MembershipType LIKE @plan";

                    sql += " ORDER BY Date(DateOfTransaction) ASC, PaymentID ASC";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@start", startDate);
                        cmd.Parameters.AddWithValue("@end", endDate);
                        if (planFilter != "All Plans")
                            cmd.Parameters.AddWithValue("@plan", "%" + planFilter + "%");

                        using (var reader = cmd.ExecuteReader())
                        {
                        var refunds = new List<RefundTransaction>();
                        int refundNumber = 1;

                        while (reader.Read())
                        {
                            double rawAmount = reader["TotalAmount"] != DBNull.Value
                                ? Convert.ToDouble(reader["TotalAmount"])
                                : 0;
                            double refundAmount = Math.Abs(rawAmount);

                            var refund = new RefundTransaction
                            {
                                RefundId = $"RF-{refundNumber:D5}",
                                MemberId = reader["MemberID"]?.ToString() ?? string.Empty,
                                MemberName = reader["MemberName"]?.ToString() ?? string.Empty,
                                PlanName = CleanPlanName(reader["MembershipType"]?.ToString() ?? string.Empty),
                                OriginalAmount = refundAmount,
                                RefundAmount = refundAmount,
                                ProcessedBy = reader["ProcessedBy"]?.ToString() ?? "System"
                            };

                            SetRefundDate(refund, reader["DateOfTransaction"]?.ToString() ?? string.Empty);

                            refunds.Add(refund);
                            TotalRefundsAmount += refundAmount;
                            refundNumber++;
                        }

                        for (int i = refunds.Count - 1; i >= 0; i--)
                        {
                            RefundTransactions.Add(refunds[i]);
                        }
                        }
                    }
                }

                OnPropertyChanged(nameof(TotalRefundsCount));
                OnPropertyChanged(nameof(TotalRefundsAmount));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading refund reports: " + ex.Message,
                    "Refund Reports", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string CleanPlanName(string planName)
        {
            const string refundPrefix = "[REFUND]";
            const string refundedPrefix = "[REFUNDED]";
            if (planName.StartsWith(refundPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return planName.Substring(refundPrefix.Length).Trim();
            }
            if (planName.StartsWith(refundedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return planName.Substring(refundedPrefix.Length).Trim();
            }

            return string.IsNullOrWhiteSpace(planName) ? "Refund" : planName;
        }

        private static void SetRefundDate(RefundTransaction refund, string rawDate)
        {
            if (DateTime.TryParse(rawDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
            {
                refund.FormattedDate = parsedDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                refund.FormattedTime = rawDate.Contains(":")
                    ? parsedDate.ToString("hh:mm tt", CultureInfo.InvariantCulture)
                    : string.Empty;
                return;
            }

            refund.FormattedDate = rawDate;
            refund.FormattedTime = string.Empty;
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (RefundTransactions.Count == 0)
            {
                MessageBox.Show("There are no refund transactions to export.",
                    "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Refund Report",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"refund-report-{DateTime.Now:yyyyMMdd}.csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var gymProfile = DatabaseHelper.GetGymProfile();
                string gymName = gymProfile.ContainsKey("GymName") ? gymProfile["GymName"] : "Gym";
                string gymAddress = gymProfile.ContainsKey("Address") ? gymProfile["Address"] : "";
                string gymContact = gymProfile.ContainsKey("ContactNumber") ? gymProfile["ContactNumber"] : "";
                string gymEmail = gymProfile.ContainsKey("Email") ? gymProfile["Email"] : "";

                string startDate = StartDatePicker.SelectedDate?.ToString("yyyy-MM-dd") ?? "N/A";
                string endDate = EndDatePicker.SelectedDate?.ToString("yyyy-MM-dd") ?? "N/A";
                string planFilter = (PlanFilterComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All Plans";

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
                    writer.WriteLine("Report Type,Refund Report");
                    writer.WriteLine($"Date Generated,{Csv(DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"))}");
                    writer.WriteLine($"Filter Period,{Csv($"{startDate} to {endDate}")}");
                    writer.WriteLine($"Plan Filter,{Csv(planFilter)}");
                    writer.WriteLine($"Total Refunded Amount,{Csv($"₱{TotalRefundsAmount:N2}")}");
                    writer.WriteLine($"Total Refund Transactions,{Csv(TotalRefundsCount.ToString())}");
                    writer.WriteLine();

                    // 3. Report Data Block
                    writer.WriteLine("REPORT DATA");
                    writer.WriteLine("Refund ID,Date,Time,Member ID,Member Name,Plan,Original Amount,Refund Amount,Processed By");

                    foreach (var refund in RefundTransactions)
                    {
                        writer.WriteLine(string.Join(",",
                            Csv(refund.RefundId),
                            Csv(refund.FormattedDate),
                            Csv(refund.FormattedTime),
                            Csv(refund.MemberId),
                            Csv(refund.MemberName),
                            Csv(refund.PlanName),
                            refund.OriginalAmount.ToString("F2", CultureInfo.InvariantCulture),
                            refund.RefundAmount.ToString("F2", CultureInfo.InvariantCulture),
                            Csv(refund.ProcessedBy)));
                    }
                }

                MessageBox.Show("Refund report exported successfully.",
                    "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exporting refund report: " + ex.Message,
                    "Export CSV", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string Csv(string value)
        {
            string safeValue = value.Replace("\"", "\"\"");
            return $"\"{safeValue}\"";
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
