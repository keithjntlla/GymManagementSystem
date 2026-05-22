using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Text;
using System.Transactions;
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
using Microsoft.Win32;

namespace GymManagementSystem.Views.Reports;

public partial class FinancialReportsView : UserControl
{
    public ObservableCollection<PaymentRecord> Transactions { get; set; }
        = new ObservableCollection<PaymentRecord>();

    public FinancialReportsView()
    {
        InitializeComponent();

        dpStart.SelectedDate = DateTime.Now.AddMonths(-1);
        dpEnd.SelectedDate = DateTime.Now;

        dgTransactions.ItemsSource = Transactions;

        LoadPlanFilterComboBox();
        LoadFinancialData();
    }

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

                // Explicitly selecting all columns from the Payments database tracking schema
                var sb = new StringBuilder(
                    @"SELECT * FROM Payments
                      WHERE DateOfTransaction BETWEEN @start AND @end
                        AND IFNULL(PaymentMode, '') <> 'Refund'
                        AND IFNULL(PaymentMode, '') <> 'Refunded'
                        AND IFNULL(MembershipType, '') NOT LIKE '[REFUND]%'
                        AND IFNULL(MembershipType, '') NOT LIKE '[REFUNDED]%'");

                if (planFilter != "All Plans")
                    sb.Append(" AND MembershipType LIKE @plan"); // Uses LIKE to safely match dynamic text logs like "Monthly (x2)"

                sb.Append(" ORDER BY DateOfTransaction DESC");

                using (var cmd = new SQLiteCommand(sb.ToString(), conn))
                {
                    cmd.Parameters.AddWithValue("@start", startDate);
                    cmd.Parameters.AddWithValue("@end", endDate);
                    if (planFilter != "All Plans")
                        cmd.Parameters.AddWithValue("@plan", "%" + planFilter + "%");

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

                                // ── FIXED: EXTRACT NET VALUE FROM THE TRANSACTION ────────
                                TotalAmount = reader["TotalAmount"] != DBNull.Value
                                    ? Convert.ToDouble(reader["TotalAmount"])
                                    : Convert.ToDouble(reader["AmountPaid"]), // Fallback calculation protection for legacy logs

                                PaymentMode = reader["PaymentMode"]?.ToString() ?? string.Empty,
                                MembershipType = reader["MembershipType"]?.ToString() ?? string.Empty,
                                DateOfTransaction = reader["DateOfTransaction"]?.ToString() ?? string.Empty,
                            };

                            Transactions.Add(record);

                            // ── FIXED: ACCUMULATE THE TRUE RATE VALUE (LESS THE CHANGE) ────
                            totalRevenue += record.TotalAmount;

                            if (record.MembershipType.StartsWith("Daily", StringComparison.OrdinalIgnoreCase))
                            {
                                walkInRevenue += record.TotalAmount;
                                walkInCount++;
                            }
                            else
                            {
                                subscriptionRevenue += record.TotalAmount;
                                subscriptionCount++;
                            }
                        }
                    }
                }
            }

            lblTotalRevenue.Text = $"₱{totalRevenue:N2}"; // Switched to N2 to accurately present cents decimals if discounts apply
            lblWalkIns.Text = $"₱{walkInRevenue:N2}";
            lblWalkInsCount.Text = $"{walkInCount} transactions";
            lblSubscriptions.Text = $"₱{subscriptionRevenue:N2}";
            lblSubscriptionsCount.Text = $"{subscriptionCount} transactions";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error loading financial data: " + ex.Message);
        }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (Transactions.Count == 0)
        {
            MessageBox.Show("There are no financial transactions to export.",
                "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export Financial Report",
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"financial-report-{DateTime.Now:yyyyMMdd}.csv"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            using (var writer = new StreamWriter(dialog.FileName))
            {
                writer.WriteLine("Date,Member ID,Member Name,Plan,Amount Paid,Total Amount,Payment Mode");
                foreach (var transaction in Transactions)
                {
                    writer.WriteLine(string.Join(",",
                        Csv(transaction.DateOfTransaction),
                        Csv(transaction.MemberID),
                        Csv(transaction.MemberName),
                        Csv(transaction.MembershipType),
                        transaction.AmountPaid.ToString("F2", CultureInfo.InvariantCulture),
                        transaction.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
                        Csv(transaction.PaymentMode)));
                }
            }

            MessageBox.Show("Financial report exported successfully.",
                "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error exporting financial report: " + ex.Message,
                "Export CSV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string Csv(string value)
    {
        string safeValue = value.Replace("\"", "\"\"");
        return $"\"{safeValue}\"";
    }
}
