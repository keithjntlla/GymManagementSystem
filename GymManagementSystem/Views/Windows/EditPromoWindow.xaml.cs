using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using GymManagementSystem.Models;

namespace GymManagementSystem.Views.Windows
{
    public partial class EditPromoWindow : Window
    {
        private bool isEditMode = false;
        private int editPromoId = 0;
        private ValidationHelper _validationHelper = null!;
        private List<RateSelectionWrapper> ratesList = new List<RateSelectionWrapper>();

        public EditPromoWindow()
        {
            InitializeComponent();
            isEditMode = false;
            lblTitle.Text = "Create Promo Code";
            btnSave.Content = "Create";
            
            // Set default dates
            dpStartDate.SelectedDate = DateTime.Today;
            dpEndDate.SelectedDate = DateTime.Today.AddMonths(1);

            LoadRatesList();
            InitializeValidation();
        }

        public EditPromoWindow(Promo promoToEdit)
        {
            InitializeComponent();
            isEditMode = true;
            editPromoId = promoToEdit.PromoID;
            lblTitle.Text = "Edit Promo Code";
            btnSave.Content = "Update";

            txtPromoCode.Text = promoToEdit.PromoCode;
            txtPromoCode.IsEnabled = false; // Cannot change code after creation
            txtPromoName.Text = promoToEdit.PromoName;
            
            cmbDiscountType.Text = promoToEdit.DiscountType == "FixedAmount" ? "Fixed Amount" : "Percentage";
            txtDiscountValue.Text = promoToEdit.DiscountValue.ToString();

            if (DateTime.TryParse(promoToEdit.StartDate, out DateTime start))
                dpStartDate.SelectedDate = start;
            if (DateTime.TryParse(promoToEdit.EndDate, out DateTime end))
                dpEndDate.SelectedDate = end;

            LoadRatesList();

            // Set applicable rates check state
            if (promoToEdit.ApplicableRates == "All")
            {
                chkAllRates.IsChecked = true;
            }
            else
            {
                chkAllRates.IsChecked = false;
                var scopes = promoToEdit.ApplicableRates.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var scopesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in scopes) scopesSet.Add(s.Trim());

                foreach (var r in ratesList)
                {
                    if (scopesSet.Contains(r.PlanName))
                    {
                        r.IsSelected = true;
                    }
                }
            }

            InitializeValidation();
        }

        private void LoadRatesList()
        {
            ratesList.Clear();
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT PlanName FROM Rates ORDER BY PlanName ASC", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            ratesList.Add(new RateSelectionWrapper
                            {
                                PlanName = r["PlanName"]?.ToString() ?? "",
                                IsSelected = false
                            });
                        }
                    }
                }
            }
            catch { }
            icMembershipRates.ItemsSource = ratesList;
        }

        private void InitializeValidation()
        {
            _validationHelper = new ValidationHelper();

            _validationHelper.RegisterTextBox(txtPromoCode, lblPromoCodeError, input =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return (false, "", "Promo Code is required.");

                string cleaned = input.Trim().ToUpper();
                if (cleaned.Length < 3 || cleaned.Length > 20)
                    return (false, "", "Promo Code must be between 3 and 20 characters.");

                if (!System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^[A-Z0-9_]+$"))
                    return (false, "", "Promo Code can only contain letters, numbers, and underscores.");

                if (!isEditMode && IsPromoCodeDuplicate(cleaned))
                    return (false, "", "This promo code already exists in your database.");

                return (true, cleaned, "");
            });

            _validationHelper.RegisterTextBox(txtPromoName, lblPromoNameError, input =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return (false, "", "Campaign Name is required.");

                string cleaned = input.Trim();
                if (cleaned.Length < 3 || cleaned.Length > 100)
                    return (false, "", "Campaign Name must be between 3 and 100 characters.");

                return (true, cleaned, "");
            });

            _validationHelper.RegisterTextBox(txtDiscountValue, lblDiscountValueError, input =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return (false, "", "Discount Value is required.");

                if (!double.TryParse(input.Trim(), out double val) || val <= 0)
                    return (false, "", "Discount Value must be a valid positive number.");

                string discType = (cmbDiscountType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Percentage";
                if (discType.Contains("Percentage") && val > 100)
                    return (false, "", "Percentage value cannot exceed 100%.");

                return (true, val.ToString("0.##"), "");
            });

            _validationHelper.RegisterDatePicker(dpStartDate, lblStartDateError, input =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return (false, "", "Start Date is required.");

                return (true, input, "");
            });

            _validationHelper.RegisterDatePicker(dpEndDate, lblEndDateError, input =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return (false, "", "End Date is required.");

                if (dpStartDate.SelectedDate.HasValue && dpEndDate.SelectedDate.HasValue && 
                    dpEndDate.SelectedDate.Value < dpStartDate.SelectedDate.Value)
                {
                    return (false, input, "End Date cannot be before the Start Date.");
                }

                return (true, input, "");
            });
        }

        private bool IsPromoCodeDuplicate(string code)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT COUNT(*) FROM Promos WHERE UPPER(PromoCode) = UPPER(@code) AND PromoID != @id";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@code", code);
                        cmd.Parameters.AddWithValue("@id", editPromoId);
                        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
                    }
                }
            }
            catch { return false; }
        }

        private void DiscountType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lblDiscountValHeader == null) return;

            string discType = (cmbDiscountType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Percentage";
            lblDiscountValHeader.Text = discType.Contains("Percentage") ? "Discount Value (%)" : "Discount Value (₱)";
            
            // Re-trigger validation for value
            if (txtDiscountValue != null && _validationHelper != null)
            {
                _validationHelper.ValidateAll();
            }
        }

        private void ChkAllRates_Changed(object sender, RoutedEventArgs e)
        {
            if (icMembershipRates == null) return;
            icMembershipRates.IsEnabled = chkAllRates.IsChecked != true;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!_validationHelper.ValidateAll()) return;

            string code = txtPromoCode.Text.Trim().ToUpper();
            string name = txtPromoName.Text.Trim();
            string discType = (cmbDiscountType.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Fixed Amount" ? "FixedAmount" : "Percentage";
            double val = Convert.ToDouble(txtDiscountValue.Text.Trim());
            string start = (dpStartDate.SelectedDate ?? DateTime.Today).ToString("yyyy-MM-dd");
            string end = (dpEndDate.SelectedDate ?? DateTime.Today.AddMonths(1)).ToString("yyyy-MM-dd");

            string scope = "All";
            if (chkAllRates.IsChecked != true)
            {
                var scopesList = new List<string>();
                foreach (var r in ratesList)
                {
                    if (r.IsSelected) scopesList.Add(r.PlanName);
                }

                if (scopesList.Count == 0)
                {
                    MessageBox.Show("Please select at least one applicable membership rate, or check 'Apply to all membership rates'.", 
                                    "Rate Scope Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                scope = string.Join(",", scopesList);
            }

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql;
                    if (isEditMode)
                    {
                        sql = @"UPDATE Promos SET 
                                PromoName = @name, DiscountType = @type, DiscountValue = @val, 
                                StartDate = @start, EndDate = @end, ApplicableRates = @scope
                                WHERE PromoID = @id";
                    }
                    else
                    {
                        sql = @"INSERT INTO Promos 
                                (PromoCode, PromoName, DiscountType, DiscountValue, StartDate, EndDate, ApplicableRates, IsActive, IsArchived)
                                VALUES 
                                (@code, @name, @type, @val, @start, @end, @scope, 1, 0)";
                    }

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        if (isEditMode)
                        {
                            cmd.Parameters.AddWithValue("@id", editPromoId);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@code", code);
                        }
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@type", discType);
                        cmd.Parameters.AddWithValue("@val", val);
                        cmd.Parameters.AddWithValue("@start", start);
                        cmd.Parameters.AddWithValue("@end", end);
                        cmd.Parameters.AddWithValue("@scope", scope);
                        cmd.ExecuteNonQuery();
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving promo code: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
