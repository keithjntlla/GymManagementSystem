using GymManagementSystem.Models;
using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace GymManagementSystem.Views
{
    public partial class RateSettingsView : UserControl
    {
        public ObservableCollection<GymPlan> RatesList { get; set; } = new ObservableCollection<GymPlan>();
        public ObservableCollection<GymPlan> ArchivedRatesList { get; set; } = new ObservableCollection<GymPlan>();

        public RateSettingsView()
        {
            InitializeComponent();
            DatabaseHelper.MigrateRatesTable();
            LoadRates();
        }

        private void LoadRates()
        {
            RatesList.Clear();
            ArchivedRatesList.Clear();
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT * FROM Rates";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var plan = new GymPlan
                            {
                                RateID = Convert.ToInt32(reader["RateID"]),
                                PlanName = reader["PlanName"]?.ToString() ?? "",
                                Price = Convert.ToDouble(reader["Price"]),
                                DurationDays = Convert.ToInt32(reader["DurationDays"]),
                                IsArchived = Convert.ToInt32(reader["IsArchived"]) == 1
                            };

                            if (plan.IsArchived)
                                ArchivedRatesList.Add(plan);
                            else
                                RatesList.Add(plan);
                        }
                    }
                }
                dgRates.ItemsSource = RatesList;
                dgArchivedRates.ItemsSource = ArchivedRatesList;

                // Show/hide archived section based on whether there are archived plans
                ArchivedSection.Visibility = ArchivedRatesList.Count > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading rates: " + ex.Message);
            }
        }

        // ── ADD ───────────────────────────────────────────────────
        private void AddRate_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new EditGymPlanWindow();
            addWindow.Owner = Window.GetWindow(this);

            if (addWindow.ShowDialog() == true)
            {
                LoadRates();
            }
        }

        // ── EDIT ──────────────────────────────────────────────────
        private void EditRate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GymPlan plan)
            {
                var editWindow = new EditGymPlanWindow(plan);
                editWindow.Owner = Window.GetWindow(this);

                if (editWindow.ShowDialog() == true)
                {
                    UpdateDatabase(editWindow.EditedPlan.RateID,
                                   editWindow.EditedPlan.PlanName,
                                   editWindow.EditedPlan.Price,
                                   editWindow.EditedPlan.DurationDays);
                    LoadRates();
                }
            }
        }

        private void UpdateDatabase(int rateId, string name, double price, int duration)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "UPDATE Rates SET PlanName = @name, Price = @price, DurationDays = @duration WHERE RateID = @id";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@price", price);
                        cmd.Parameters.AddWithValue("@duration", duration);
                        cmd.Parameters.AddWithValue("@id", rateId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database Error: " + ex.Message);
            }
        }

        // ── DELETE / ARCHIVE ──────────────────────────────────────
        private void DeleteRate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GymPlan plan)
            {
                bool hasActive = DatabaseHelper.HasActiveMembers(plan.PlanName);

                if (hasActive)
                {
                    // Block hard delete — offer archive instead
                    var archiveResult = MessageBox.Show(
                        $"'{plan.PlanName}' cannot be deleted because it has active members enrolled.\n\n" +
                        $"Would you like to archive it instead? Archived plans won't be available for new sign-ups, " +
                        $"but existing members will be unaffected.",
                        "Cannot Delete — Active Members Exist",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (archiveResult == MessageBoxResult.Yes)
                    {
                        try
                        {
                            DatabaseHelper.ArchivePlan(plan.RateID);
                            LoadRates();
                            MessageBox.Show($"'{plan.PlanName}' has been archived successfully.",
                                "Plan Archived", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Database Error: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    // No active members — safe to hard delete
                    var result = MessageBox.Show(
                        $"Are you sure you want to permanently delete the plan '{plan.PlanName}'?",
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                            {
                                conn.Open();
                                string sql = "DELETE FROM Rates WHERE RateID = @id";
                                using (var cmd = new SQLiteCommand(sql, conn))
                                {
                                    cmd.Parameters.AddWithValue("@id", plan.RateID);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            LoadRates();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Database Error: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        // ── RESTORE ARCHIVED PLAN ─────────────────────────────────
        private void RestorePlan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GymPlan plan)
            {
                var result = MessageBox.Show(
                    $"Restore '{plan.PlanName}'? It will become available for new sign-ups again.",
                    "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        DatabaseHelper.RestorePlan(plan.RateID);
                        LoadRates();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Database Error: " + ex.Message,
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}