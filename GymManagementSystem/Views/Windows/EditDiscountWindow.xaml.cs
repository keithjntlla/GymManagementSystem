using GymManagementSystem.Views.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using GymManagementSystem.Models;

namespace GymManagementSystem.Views.Windows
{
    public partial class EditDiscountWindow : Window
    {
        private readonly bool _isEditMode;
        public FixedDiscount ViewModel { get; private set; }
        public ObservableCollection<RateSelectionWrapper> AvailableRates { get; set; } = new ObservableCollection<RateSelectionWrapper>();

        public string WindowTitle { get; set; }
        public string SaveButtonText { get; set; }

        private ValidationHelper _validationHelper = null!;

        public EditDiscountWindow() // ADD MODE
        {
            InitializeComponent();
            DataContext = this;
            _isEditMode = false;
            WindowTitle = "Add New Discount Tier";
            SaveButtonText = "Create Tier";
            ViewModel = new FixedDiscount { DiscountID = Guid.NewGuid().ToString(), Percentage = 0, ApplicableRates = "All" };
            LoadGymPlans();
            InitializeValidation();
        }

        public EditDiscountWindow(FixedDiscount existingDiscount) // EDIT MODE
        {
            InitializeComponent();
            DataContext = this;
            _isEditMode = true;
            WindowTitle = "Edit Tier Matrix";
            SaveButtonText = "Save Modifications";
            ViewModel = existingDiscount;

            txtTargetType.Text = ViewModel.TargetType;
            txtTargetType.IsEnabled = false; // Protect unique identity criteria keys
            txtPercentage.Text = ViewModel.Percentage.ToString();

            chkAllRates.IsChecked = string.Equals(ViewModel.ApplicableRates, "All", StringComparison.OrdinalIgnoreCase);
            LoadGymPlans();
            InitializeValidation();
        }

        private void InitializeValidation()
        {
            _validationHelper = new ValidationHelper();

            _validationHelper.RegisterTextBox(txtTargetType, lblTargetTypeError, input =>
            {
                var (isValid, cleaned, error) = InputValidator.ValidateDiscountName(input);
                if (!isValid) return (false, cleaned, error);

                if (!_isEditMode && IsDiscountNameDuplicate(cleaned))
                {
                    return (false, cleaned, "A discount tier with this name already exists.");
                }
                return (true, cleaned, "");
            });

            _validationHelper.RegisterTextBox(txtPercentage, lblPercentageError, InputValidator.ValidateDiscountPercentage);
        }

        private bool IsDiscountNameDuplicate(string targetType)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT COUNT(*) FROM Discounts WHERE LOWER(TargetType) = LOWER(@type)";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@type", targetType);
                        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
                    }
                }
            }
            catch { return false; }
        }

        private void LoadGymPlans()
        {
            AvailableRates.Clear();
            var savedScopes = ViewModel.ApplicableRates.Split(',').Select(p => p.Trim()).ToList();

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT PlanName FROM Rates WHERE IsArchived = 0", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string planName = reader["PlanName"]?.ToString() ?? "";
                            AvailableRates.Add(new RateSelectionWrapper
                            {
                                PlanName = planName,
                                IsSelected = chkAllRates.IsChecked == true || savedScopes.Contains(planName)
                            });
                        }
                    }
                }
                icMembershipRates.ItemsSource = AvailableRates;
                ChkAllRates_Changed(this, null);
            }
            catch (Exception ex) { MessageBox.Show("Failed to parse rate dependencies: " + ex.Message); }
        }

        private void ChkAllRates_Changed(object sender, RoutedEventArgs? e)
        {
            if (icMembershipRates == null) return;
            icMembershipRates.IsEnabled = chkAllRates.IsChecked == false;
            if (chkAllRates.IsChecked == true)
            {
                foreach (var rate in AvailableRates) rate.IsSelected = true;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!_validationHelper.ValidateAll())
            {
                return;
            }

            string rateConfig = "All";
            if (chkAllRates.IsChecked == false)
            {
                var selected = AvailableRates.Where(r => r.IsSelected).Select(r => r.PlanName).ToList();
                if (!selected.Any())
                {
                    MessageBox.Show("Please explicitly check at least one rate package pattern or utilize 'All'.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                rateConfig = string.Join(",", selected);
            }

            ViewModel.TargetType = txtTargetType.Text.Trim();
            ViewModel.Percentage = double.Parse(txtPercentage.Text);
            ViewModel.ApplicableRates = rateConfig;

            if (!_isEditMode)
            {
                try
                {
                    using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        using (var cmd = new SQLiteCommand("INSERT INTO Discounts (DiscountID, TargetType, Percentage, ApplicableRates) VALUES (@id, @type, @pct, @rates)", conn))
                        {
                            cmd.Parameters.AddWithValue("@id", ViewModel.DiscountID);
                            cmd.Parameters.AddWithValue("@type", ViewModel.TargetType);
                            cmd.Parameters.AddWithValue("@pct", ViewModel.Percentage);
                            cmd.Parameters.AddWithValue("@rates", ViewModel.ApplicableRates);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("Database assignment fault (Duplicate Type keys?): " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            }
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => this.DialogResult = false;
    }
}