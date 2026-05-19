using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using GymManagementSystem.Views.Windows;
using GymManagementSystem.Models;

namespace GymManagementSystem.Views.Settings
{
    public partial class DiscountSettingsView : UserControl
    {
        public ObservableCollection<FixedDiscount> FixedDiscounts { get; set; } = new ObservableCollection<FixedDiscount>();
        public ObservableCollection<FixedDiscount> ArchivedDiscountsList { get; set; } = new ObservableCollection<FixedDiscount>();

        public DiscountSettingsView()
        {
            InitializeComponent();
            DatabaseHelper.MigrateDiscountsTable(); // Protection safety verify line trace mapping execution
            LoadData();
        }

        private void LoadData()
        {
            FixedDiscounts.Clear();
            ArchivedDiscountsList.Clear();
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT * FROM Discounts", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var tier = new FixedDiscount
                            {
                                DiscountID = r["DiscountID"]?.ToString() ?? string.Empty,
                                TargetType = r["TargetType"]?.ToString() ?? string.Empty,
                                Percentage = Convert.ToDouble(r["Percentage"]),
                                ApplicableRates = r["ApplicableRates"]?.ToString() ?? "All",
                                // Safely handle legacy databases before migration execution parameters
                                IsArchived = r["IsArchived"] != DBNull.Value && Convert.ToInt32(r["IsArchived"]) == 1
                            };

                            if (tier.IsArchived)
                                ArchivedDiscountsList.Add(tier);
                            else
                                FixedDiscounts.Add(tier);
                        }
                    }
                }
                dgFixedDiscounts.ItemsSource = FixedDiscounts;
                dgArchivedDiscounts.ItemsSource = ArchivedDiscountsList;

                // Match RateSettings layout rule visibility conditions
                ArchivedSection.Visibility = ArchivedDiscountsList.Count > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading discount matrix data: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddDiscount_Click(object sender, RoutedEventArgs e)
        {
            var win = new EditDiscountWindow();
            win.Owner = Window.GetWindow(this);
            if (win.ShowDialog() == true)
            {
                LoadData();
            }
        }

        private void EditFixedDiscount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is FixedDiscount selectedDiscount)
            {
                var win = new EditDiscountWindow(selectedDiscount);
                win.Owner = Window.GetWindow(this);
                if (win.ShowDialog() == true)
                {
                    try
                    {
                        using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                        {
                            conn.Open();
                            string sql = "UPDATE Discounts SET Percentage = @pct, ApplicableRates = @rates WHERE DiscountID = @id";
                            using (var cmd = new SQLiteCommand(sql, conn))
                            {
                                cmd.Parameters.AddWithValue("@pct", win.ViewModel.Percentage);
                                cmd.Parameters.AddWithValue("@rates", win.ViewModel.ApplicableRates);
                                cmd.Parameters.AddWithValue("@id", win.ViewModel.DiscountID);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        LoadData();
                    }
                    catch (Exception ex) { MessageBox.Show("Error updating target configuration trace: " + ex.Message); }
                }
            }
        }

        // SWAPPED: Soft update archive action handling instead of historical database purge logic row removals
        private void ArchiveFixedDiscount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is FixedDiscount item)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to archive '{item.TargetType}'?\n\n" +
                    "Archived rules will be preserved, but won't show up when adding or editing member tier registrations.",
                    "Confirm Archive Request", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                        {
                            conn.Open();
                            using (var cmd = new SQLiteCommand("UPDATE Discounts SET IsArchived = 1 WHERE DiscountID = @id", conn))
                            {
                                cmd.Parameters.AddWithValue("@id", item.DiscountID);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        LoadData();
                    }
                    catch (Exception ex) { MessageBox.Show("Operational database execution tracking breakdown: " + ex.Message); }
                }
            }
        }

        // NEW METHOD: Re-enables the tier group, matching the restore pattern for system rates
        private void RestoreDiscountRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is FixedDiscount item)
            {
                var result = MessageBox.Show(
                    $"Restore '{item.TargetType}' logic matrix profile parameters to active production queues?",
                    "Confirm Restore Action Request", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                        {
                            conn.Open();
                            using (var cmd = new SQLiteCommand("UPDATE Discounts SET IsArchived = 0 WHERE DiscountID = @id", conn))
                            {
                                cmd.Parameters.AddWithValue("@id", item.DiscountID);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        LoadData();
                    }
                    catch (Exception ex) { MessageBox.Show("Database Error executing update query restoration tracking: " + ex.Message); }
                }
            }
        }
    }
}