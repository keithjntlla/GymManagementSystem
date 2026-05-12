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

        public RateSettingsView()
        {
            InitializeComponent();
            LoadRates();
        }

        private void LoadRates()
        {
            RatesList.Clear();
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
                            RatesList.Add(new GymPlan
                            {
                                RateID = Convert.ToInt32(reader["RateID"]),
                                PlanName = reader["PlanName"]?.ToString() ?? "",
                                Price = Convert.ToDouble(reader["Price"]),
                                DurationDays = Convert.ToInt32(reader["DurationDays"])
                            });
                        }
                    }
                }
                dgRates.ItemsSource = RatesList;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading rates: " + ex.Message);
            }
        }

        private void EditRate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GymPlan plan)
            {
                // Launch the separate modal window
                EditGymPlanWindow editWindow = new EditGymPlanWindow(plan);
                editWindow.Owner = Window.GetWindow(this);

                // If user saves changes in the window, update the database
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

        private void DeleteRate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GymPlan plan)
            {
                var result = MessageBox.Show($"Are you sure you want to delete the plan '{plan.PlanName}'?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Logic for deletion can be added here
                }
            }
        }
    }
}