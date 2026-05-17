using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
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

                            if (string.Equals(record.MembershipType, "Daily", StringComparison.OrdinalIgnoreCase))
                            {
                                walkInRevenue += record.AmountPaid;
                                walkInCount++;
                            }
                            else
                            {
                                subscriptionRevenue += record.AmountPaid;
                                subscriptionCount++;
                            }
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
}