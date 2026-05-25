using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Views.Windows;

namespace GymManagementSystem.Views.Settings
{
    public partial class PromosSettingsView : UserControl
    {
        public ObservableCollection<Promo> PromosList { get; set; } = new ObservableCollection<Promo>();
        public ObservableCollection<Promo> ArchivedPromosList { get; set; } = new ObservableCollection<Promo>();
        private ICollectionView? _promosView;

        public PromosSettingsView()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            PromosList.Clear();
            ArchivedPromosList.Clear();
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT * FROM Promos ORDER BY PromoID DESC", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var promo = new Promo
                            {
                                PromoID = Convert.ToInt32(reader["PromoID"]),
                                PromoCode = reader["PromoCode"]?.ToString() ?? string.Empty,
                                PromoName = reader["PromoName"]?.ToString() ?? string.Empty,
                                DiscountType = reader["DiscountType"]?.ToString() ?? "Percentage",
                                DiscountValue = Convert.ToDouble(reader["DiscountValue"] ?? 0),
                                StartDate = reader["StartDate"]?.ToString() ?? string.Empty,
                                EndDate = reader["EndDate"]?.ToString() ?? string.Empty,
                                ApplicableRates = reader["ApplicableRates"]?.ToString() ?? "All",
                                IsActive = reader["IsActive"] == DBNull.Value || Convert.ToInt32(reader["IsActive"]) == 1,
                                IsArchived = reader["IsArchived"] != DBNull.Value && Convert.ToInt32(reader["IsArchived"]) == 1
                            };

                            if (promo.IsArchived)
                                ArchivedPromosList.Add(promo);
                            else
                                PromosList.Add(promo);
                        }
                    }
                }

                _promosView = CollectionViewSource.GetDefaultView(PromosList);
                _promosView.Filter = PromoFilterLogic;

                dgPromos.ItemsSource = PromosList;
                dgArchivedPromos.ItemsSource = ArchivedPromosList;

                ArchivedSection.Visibility = ArchivedPromosList.Count > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading promos data: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool PromoFilterLogic(object obj)
        {
            if (obj is Promo promo)
            {
                string query = txtSearch.Text.Trim().ToLower();
                if (string.IsNullOrEmpty(query)) return true;

                return promo.PromoCode.ToLower().Contains(query) || 
                       promo.PromoName.ToLower().Contains(query);
            }
            return false;
        }

        private void Filter_Changed(object sender, TextChangedEventArgs e)
        {
            _promosView?.Refresh();
        }

        private void AddPromo_Click(object sender, RoutedEventArgs e)
        {
            var win = new EditPromoWindow();
            win.Owner = Window.GetWindow(this);
            if (win.ShowDialog() == true)
            {
                LoadData();
            }
        }

        private void EditPromo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Promo promo)
            {
                var win = new EditPromoWindow(promo);
                win.Owner = Window.GetWindow(this);
                if (win.ShowDialog() == true)
                {
                    LoadData();
                }
            }
        }

        private void StatusToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle && toggle.DataContext is Promo promo)
            {
                bool newIsChecked = toggle.IsChecked == true;
                try
                {
                    using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        string sql = "UPDATE Promos SET IsActive = @active WHERE PromoID = @id";
                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@active", newIsChecked ? 1 : 0);
                            cmd.Parameters.AddWithValue("@id", promo.PromoID);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error updating promo status: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    toggle.IsChecked = !newIsChecked;
                }
            }
        }

        private void ArchivePromo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Promo promo)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to archive promo '{promo.PromoCode}'?\n\n" +
                    "Archived codes will be hidden from new member checkouts but preserved for billing logs.",
                    "Confirm Archive", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                        {
                            conn.Open();
                            string sql = "UPDATE Promos SET IsArchived = 1 WHERE PromoID = @id";
                            using (var cmd = new SQLiteCommand(sql, conn))
                            {
                                cmd.Parameters.AddWithValue("@id", promo.PromoID);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        LoadData();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error archiving promo: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void RestorePromo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Promo promo)
            {
                var result = MessageBox.Show(
                    $"Restore promo '{promo.PromoCode}' back to active status?",
                    "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                        {
                            conn.Open();
                            string sql = "UPDATE Promos SET IsArchived = 0, IsActive = 1 WHERE PromoID = @id";
                            using (var cmd = new SQLiteCommand(sql, conn))
                            {
                                cmd.Parameters.AddWithValue("@id", promo.PromoID);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        LoadData();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error restoring promo: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
