using GymManagementSystem.Models;
using System;
using System.ComponentModel;
using System.Data.SQLite;
using System.Windows;

namespace GymManagementSystem.Views.Windows
{
    public partial class EditGymPlanWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly bool _isEditMode;

        public GymPlan EditedPlan { get; private set; }

        private string? _windowTitle;
        public string? WindowTitle
        {
            get => _windowTitle;
            set { _windowTitle = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WindowTitle))); }
        }

        private string? _saveButtonText;
        public string? SaveButtonText
        {
            get => _saveButtonText;
            set { _saveButtonText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveButtonText))); }
        }

        // ── ADD mode ──────────────────────────────────────────────
        public EditGymPlanWindow()
        {
            InitializeComponent();
            DataContext = this;

            _isEditMode = false;
            WindowTitle = "Add New Plan";
            SaveButtonText = "Add Plan";

            EditedPlan = new GymPlan();
        }

        // ── EDIT mode ─────────────────────────────────────────────
        public EditGymPlanWindow(GymPlan plan)
        {
            InitializeComponent();
            DataContext = this;

            _isEditMode = true;
            WindowTitle = "Edit Plan Details";
            SaveButtonText = "Save Changes";

            EditedPlan = plan;
            txtPlanName.Text = plan.PlanName;
            txtPrice.Text = plan.Price.ToString();
            txtDuration.Text = plan.DurationDays.ToString();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(txtPrice.Text, out double price) ||
                !int.TryParse(txtDuration.Text, out int duration))
            {
                MessageBox.Show("Please enter valid numbers for Price and Duration.",
                                "Validation Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtPlanName.Text))
            {
                MessageBox.Show("Plan Name cannot be empty.",
                                "Validation Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return;
            }

            if (_isEditMode)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to save changes to '{txtPlanName.Text}'?",
                    "Confirm Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                EditedPlan.PlanName = txtPlanName.Text;
                EditedPlan.Price = price;
                EditedPlan.DurationDays = duration;
            }
            else
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to add '{txtPlanName.Text}' as a new plan?",
                    "Confirm Add",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                EditedPlan.PlanName = txtPlanName.Text;
                EditedPlan.Price = price;
                EditedPlan.DurationDays = duration;

                try
                {
                    using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        string sql = @"INSERT INTO Rates (PlanName, Price, DurationDays)
                                       VALUES (@name, @price, @duration);
                                       SELECT last_insert_rowid();";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@name", EditedPlan.PlanName);
                            cmd.Parameters.AddWithValue("@price", EditedPlan.Price);
                            cmd.Parameters.AddWithValue("@duration", EditedPlan.DurationDays);

                            EditedPlan.RateID = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Database Error: " + ex.Message,
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => this.DialogResult = false;
    }
}