using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using GymManagementSystem.Models;
using Microsoft.Win32;

namespace GymManagementSystem.Views.Reports
{
    public partial class FinancialReportsView : UserControl
    {
        private List<PaymentRecord> _allTransactions = new List<PaymentRecord>();
        public ObservableCollection<PaymentRecord> Transactions { get; set; }
            = new ObservableCollection<PaymentRecord>();

        private List<CheckBox> _dynamicTypeCheckBoxes = new List<CheckBox>();
        private List<CheckBox> _dynamicPlanCheckBoxes = new List<CheckBox>();
        private bool _isUpdatingDatesFromPeriod = false;

        public FinancialReportsView()
        {
            InitializeComponent();

            dgTransactions.ItemsSource = Transactions;

            // Set initial dates (default to current month, with Custom selected and enabled)
            DateTime today = DateTime.Today;
            dpStart.SelectedDate = new DateTime(today.Year, today.Month, 1);
            dpEnd.SelectedDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
            dpStart.IsEnabled = true;
            dpEnd.IsEnabled = true;

            LoadDynamicMemberTypes();
            LoadDynamicPlans();
            LoadFinancialData();
        }

        private void LoadDynamicMemberTypes()
        {
            spDynamicTypes.Children.Clear();
            _dynamicTypeCheckBoxes.Clear();

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT DISTINCT TargetType FROM Discounts", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string tierName = reader["TargetType"]?.ToString() ?? "";
                            if (!string.IsNullOrWhiteSpace(tierName) && 
                                !tierName.Equals("Regular", StringComparison.OrdinalIgnoreCase) && 
                                !tierName.Equals("Student", StringComparison.OrdinalIgnoreCase))
                            {
                                var chk = new CheckBox
                                {
                                    Content = tierName,
                                    Style = (Style)FindResource("PopupCheckBox")
                                };
                                chk.Checked += FilterCheckbox_Changed;
                                chk.Unchecked += FilterCheckbox_Changed;
                                
                                spDynamicTypes.Children.Add(chk);
                                _dynamicTypeCheckBoxes.Add(chk);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to pull lookups for member type filter: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDynamicPlans()
        {
            spDynamicPlans.Children.Clear();
            _dynamicPlanCheckBoxes.Clear();

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT DISTINCT PlanName FROM Rates WHERE IsArchived = 0 ORDER BY DurationDays ASC", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string planName = reader["PlanName"]?.ToString() ?? "";
                            if (!string.IsNullOrWhiteSpace(planName))
                            {
                                var chk = new CheckBox
                                {
                                    Content = planName,
                                    Style = (Style)FindResource("PopupCheckBox")
                                };
                                chk.Checked += FilterCheckbox_Changed;
                                chk.Unchecked += FilterCheckbox_Changed;

                                spDynamicPlans.Children.Add(chk);
                                _dynamicPlanCheckBoxes.Add(chk);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to pull lookups for plan filter: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUpdatingDatesFromPeriod && cbPeriod != null)
            {
                cbPeriod.SelectedIndex = 3; // Reset to "Custom"
            }
            if (IsLoaded) LoadFinancialData();
        }

        private void Period_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            var selectedItem = (cbPeriod.SelectedItem as ComboBoxItem)?.Content.ToString();
            
            // DatePickers must never be disabled under any period selection
            dpStart.IsEnabled = true;
            dpEnd.IsEnabled = true;

            if (selectedItem == "Custom")
            {
                return;
            }

            _isUpdatingDatesFromPeriod = true;
            try
            {
                DateTime today = DateTime.Today;
                if (selectedItem == "This Week")
                {
                    int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                    DateTime startOfWeek = today.AddDays(-1 * diff).Date;
                    dpStart.SelectedDate = startOfWeek;
                    dpEnd.SelectedDate = startOfWeek.AddDays(6);
                }
                else if (selectedItem == "This Month")
                {
                    dpStart.SelectedDate = new DateTime(today.Year, today.Month, 1);
                    dpEnd.SelectedDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                }
                else if (selectedItem == "This Year")
                {
                    dpStart.SelectedDate = new DateTime(today.Year, 1, 1);
                    dpEnd.SelectedDate = new DateTime(today.Year, 12, 31);
                }
            }
            finally
            {
                _isUpdatingDatesFromPeriod = false;
            }
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            popFilter.IsOpen = !popFilter.IsOpen;
        }

        private void FilterCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateFilterChips();
            ApplyFilters();
        }

        private FrameworkElement CreateChip(string text, CheckBox associatedCheckBox)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1a1c")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff6b00")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 8, 4),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            stackPanel.Children.Add(textBlock);

            var closeBtn = new Button
            {
                Content = "✕",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff6b00")),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Padding = new Thickness(2, 0, 2, 0)
            };

            closeBtn.Template = new ControlTemplate(typeof(Button))
            {
                VisualTree = new FrameworkElementFactory(typeof(ContentPresenter))
            };

            closeBtn.MouseEnter += (s, e) => closeBtn.Foreground = Brushes.Red;
            closeBtn.MouseLeave += (s, e) => closeBtn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff6b00"));

            closeBtn.Click += (s, e) =>
            {
                associatedCheckBox.IsChecked = false;
            };

            stackPanel.Children.Add(closeBtn);
            border.Child = stackPanel;

            return border;
        }

        private void UpdateFilterChips()
        {
            wpChips.Children.Clear();

            // Mode category
            if (chkModeCash.IsChecked == true) wpChips.Children.Add(CreateChip("Cash", chkModeCash));
            if (chkModeGCash.IsChecked == true) wpChips.Children.Add(CreateChip("GCash", chkModeGCash));

            // Member Type category
            if (chkTypeRegular.IsChecked == true) wpChips.Children.Add(CreateChip("Regular", chkTypeRegular));
            if (chkTypeStudent.IsChecked == true) wpChips.Children.Add(CreateChip("Student", chkTypeStudent));

            // Discount category
            if (chkDiscountedOnly.IsChecked == true) wpChips.Children.Add(CreateChip("Discounted Only", chkDiscountedOnly));
            if (chkNoDiscount.IsChecked == true) wpChips.Children.Add(CreateChip("No Discount", chkNoDiscount));

            // Dynamic types
            foreach (var chk in _dynamicTypeCheckBoxes)
            {
                if (chk.IsChecked == true)
                {
                    wpChips.Children.Add(CreateChip(chk.Content?.ToString() ?? "Type", chk));
                }
            }

            // Dynamic plans
            foreach (var chk in _dynamicPlanCheckBoxes)
            {
                if (chk.IsChecked == true)
                {
                    wpChips.Children.Add(CreateChip(chk.Content?.ToString() ?? "Plan", chk));
                }
            }
        }

        private void LoadFinancialData()
        {
            if (dpStart.SelectedDate == null || dpEnd.SelectedDate == null) return;

            string startDate = dpStart.SelectedDate.Value.ToString("yyyy-MM-dd");
            string endDate = dpEnd.SelectedDate.Value.ToString("yyyy-MM-dd");

            _allTransactions.Clear();

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    string sql = @"
                        SELECT P.*, COALESCE(M.MemberType, 'Regular') AS MemberType, MP.PromoCode 
                        FROM Payments P
                        LEFT JOIN Members M ON P.MemberID = M.MemberID
                        LEFT JOIN MemberPromos MP ON P.PaymentID = MP.PaymentID
                        WHERE P.DateOfTransaction BETWEEN @start AND @end
                          AND IFNULL(P.PaymentMode, '') <> 'Refund'
                          AND IFNULL(P.PaymentMode, '') <> 'Refunded'
                          AND IFNULL(P.MembershipType, '') NOT LIKE '[REFUND]%'
                          AND IFNULL(P.MembershipType, '') NOT LIKE '[REFUNDED]%'
                        ORDER BY P.DateOfTransaction DESC";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@start", startDate);
                        cmd.Parameters.AddWithValue("@end", endDate);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var record = new PaymentRecord
                                {
                                    PaymentID = Convert.ToInt32(reader["PaymentID"]),
                                    MemberID = reader["MemberID"]?.ToString() ?? string.Empty,
                                    MemberName = reader["MemberName"]?.ToString() ?? string.Empty,
                                    MemberType = reader["MemberType"]?.ToString() ?? "Regular",
                                    AmountPaid = Convert.ToDouble(reader["AmountPaid"]),
                                    TotalAmount = reader["TotalAmount"] != DBNull.Value
                                        ? Convert.ToDouble(reader["TotalAmount"])
                                        : Convert.ToDouble(reader["AmountPaid"]),
                                    PaymentMode = reader["PaymentMode"]?.ToString() ?? string.Empty,
                                    MembershipType = reader["MembershipType"]?.ToString() ?? string.Empty,
                                    DateOfTransaction = reader["DateOfTransaction"]?.ToString() ?? string.Empty,
                                    DiscountAmount = reader["DiscountAmount"] != DBNull.Value ? Convert.ToDouble(reader["DiscountAmount"]) : 0,
                                    PromoCode = reader["PromoCode"]?.ToString() ?? string.Empty
                                };

                                _allTransactions.Add(record);
                            }
                        }
                    }
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading financial data: " + ex.Message);
            }
        }

        private void ApplyFilters()
        {
            Transactions.Clear();
            double totalRevenue = 0;
            double walkInRevenue = 0;
            int walkInCount = 0;
            double subscriptionRevenue = 0;
            int subscriptionCount = 0;
            double totalDiscounts = 0;
            int discountedCount = 0;

            // 1. Payment Mode Category (OR within category, active if any is checked)
            bool isModeFilterActive = chkModeCash.IsChecked == true || chkModeGCash.IsChecked == true;
            
            // 2. Member Type Category (OR within category, active if any is checked)
            bool isTypeFilterActive = chkTypeRegular.IsChecked == true || chkTypeStudent.IsChecked == true;
            foreach (var chk in _dynamicTypeCheckBoxes)
            {
                if (chk.IsChecked == true)
                {
                    isTypeFilterActive = true;
                    break;
                }
            }

            // 3. Plan Category (OR within category, active if any is checked)
            bool isPlanFilterActive = false;
            foreach (var chk in _dynamicPlanCheckBoxes)
            {
                if (chk.IsChecked == true)
                {
                    isPlanFilterActive = true;
                    break;
                }
            }

            // 4. Discount Status Category (OR within category, active if any is checked)
            bool isDiscountFilterActive = chkDiscountedOnly.IsChecked == true || chkNoDiscount.IsChecked == true;

            foreach (var record in _allTransactions)
            {
                // Mode Match
                if (isModeFilterActive)
                {
                    bool modeMatched = false;
                    if (chkModeCash.IsChecked == true && record.PaymentMode.Equals("Cash", StringComparison.OrdinalIgnoreCase)) modeMatched = true;
                    if (chkModeGCash.IsChecked == true && record.PaymentMode.Equals("GCash", StringComparison.OrdinalIgnoreCase)) modeMatched = true;
                    if (!modeMatched) continue;
                }

                // Member Type Match
                if (isTypeFilterActive)
                {
                    bool typeMatched = false;
                    if (chkTypeRegular.IsChecked == true && record.MemberType.Equals("Regular", StringComparison.OrdinalIgnoreCase)) typeMatched = true;
                    if (chkTypeStudent.IsChecked == true && record.MemberType.Equals("Student", StringComparison.OrdinalIgnoreCase)) typeMatched = true;
                    foreach (var chk in _dynamicTypeCheckBoxes)
                    {
                        if (chk.IsChecked == true && record.MemberType.Equals(chk.Content?.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            typeMatched = true;
                            break;
                        }
                    }
                    if (!typeMatched) continue;
                }

                // Plan Match
                if (isPlanFilterActive)
                {
                    bool planMatched = false;
                    foreach (var chk in _dynamicPlanCheckBoxes)
                    {
                        if (chk.IsChecked == true)
                        {
                            string planName = chk.Content?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(planName) && record.MembershipType.Contains(planName, StringComparison.OrdinalIgnoreCase))
                            {
                                planMatched = true;
                                break;
                            }
                        }
                    }
                    if (!planMatched) continue;
                }

                // Discount Status Match
                if (isDiscountFilterActive)
                {
                    bool discountMatched = false;
                    if (chkDiscountedOnly.IsChecked == true && record.DiscountAmount > 0) discountMatched = true;
                    if (chkNoDiscount.IsChecked == true && record.DiscountAmount <= 0) discountMatched = true;
                    if (!discountMatched) continue;
                }

                Transactions.Add(record);

                totalRevenue += record.TotalAmount;

                if (record.DiscountAmount > 0)
                {
                    totalDiscounts += record.DiscountAmount;
                    discountedCount++;
                }

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

            lblTotalRevenue.Text = $"₱{totalRevenue:N2}";
            lblWalkIns.Text = $"₱{walkInRevenue:N2}";
            lblWalkInsCount.Text = $"{walkInCount} transactions";
            lblSubscriptions.Text = $"₱{subscriptionRevenue:N2}";
            lblSubscriptionsCount.Text = $"{subscriptionCount} transactions";
            lblTotalDiscounts.Text = $"₱{totalDiscounts:N2}";
            lblTotalDiscountsCount.Text = $"{discountedCount} transactions";
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
                var gymProfile = DatabaseHelper.GetGymProfile();
                string gymName = gymProfile.ContainsKey("GymName") ? gymProfile["GymName"] : "Gym";
                string gymAddress = gymProfile.ContainsKey("Address") ? gymProfile["Address"] : "";
                string gymContact = gymProfile.ContainsKey("ContactNumber") ? gymProfile["ContactNumber"] : "";
                string gymEmail = gymProfile.ContainsKey("Email") ? gymProfile["Email"] : "";

                string startDate = dpStart.SelectedDate?.ToString("yyyy-MM-dd") ?? "N/A";
                string endDate = dpEnd.SelectedDate?.ToString("yyyy-MM-dd") ?? "N/A";

                // Generate active filter summary text for the CSV
                var activeFilters = new List<string>();
                if (chkModeCash.IsChecked == true) activeFilters.Add("Cash");
                if (chkModeGCash.IsChecked == true) activeFilters.Add("GCash");
                if (chkTypeRegular.IsChecked == true) activeFilters.Add("Regular");
                if (chkTypeStudent.IsChecked == true) activeFilters.Add("Student");
                if (chkDiscountedOnly.IsChecked == true) activeFilters.Add("Discounted Only");
                if (chkNoDiscount.IsChecked == true) activeFilters.Add("No Discount");
                foreach (var chk in _dynamicTypeCheckBoxes)
                {
                    if (chk.IsChecked == true) activeFilters.Add(chk.Content?.ToString() ?? "");
                }
                foreach (var chk in _dynamicPlanCheckBoxes)
                {
                    if (chk.IsChecked == true) activeFilters.Add(chk.Content?.ToString() ?? "");
                }
                string filterSummaryText = activeFilters.Count > 0 ? string.Join(" | ", activeFilters) : "All Filters";

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
                    writer.WriteLine("Report Type,Financial Report");
                    writer.WriteLine($"Date Generated,{Csv(DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"))}");
                    writer.WriteLine($"Filter Period,{Csv($"{startDate} to {endDate}")}");
                    writer.WriteLine($"Filters Applied,{Csv(filterSummaryText)}");
                    writer.WriteLine($"Total Revenue,{Csv(lblTotalRevenue.Text)}");
                    writer.WriteLine($"Walk-in Revenue,{Csv($"{lblWalkIns.Text} ({lblWalkInsCount.Text})")}");
                    writer.WriteLine($"Subscription Revenue,{Csv($"{lblSubscriptions.Text} ({lblSubscriptionsCount.Text})")}");
                    writer.WriteLine($"Total Discounts,{Csv($"{lblTotalDiscounts.Text} ({lblTotalDiscountsCount.Text})")}");
                    writer.WriteLine();

                    // 3. Report Data Block
                    writer.WriteLine("REPORT DATA");
                    writer.WriteLine("Date,Member ID,Member Name,Member Type,Plan,Amount Paid,Total Amount,Discount Amount,Promo Code,Payment Mode");
                    foreach (var transaction in Transactions)
                    {
                        writer.WriteLine(string.Join(",",
                            Csv(transaction.DateOfTransaction),
                            Csv(transaction.MemberID),
                            Csv(transaction.MemberName),
                            Csv(transaction.MemberType),
                            Csv(transaction.MembershipType),
                            transaction.AmountPaid.ToString("F2", CultureInfo.InvariantCulture),
                            transaction.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
                            transaction.DiscountAmount.ToString("F2", CultureInfo.InvariantCulture),
                            Csv(transaction.PromoCode),
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
}
