using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using GymManagementSystem.Models;

namespace GymManagementSystem.Views.Settings
{
    public partial class SpecializationSettingsView : UserControl
    {
        public SpecializationSettingsView()
        {
            InitializeComponent();
            LoadSpecializations();
        }

        private void LoadSpecializations()
        {
            var list = new List<Specialization>();
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT SpecializationID, Name FROM Specializations ORDER BY Name ASC";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Specialization
                            {
                                SpecializationID = Convert.ToInt32(reader["SpecializationID"]),
                                Name = reader["Name"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
                dgSpecializations.ItemsSource = list;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading specializations: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddSpecialization_Click(object sender, RoutedEventArgs e)
        {
            lblSpecError.Visibility = Visibility.Collapsed;
            string specName = txtSpecName.Text.Trim();

            if (string.IsNullOrEmpty(specName))
            {
                lblSpecError.Text = "Specialization name cannot be empty.";
                lblSpecError.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // Check for duplicate
                    string checkSql = "SELECT COUNT(*) FROM Specializations WHERE LOWER(Name) = LOWER(@name)";
                    using (var checkCmd = new SQLiteCommand(checkSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@name", specName);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar() ?? 0);
                        if (count > 0)
                        {
                            lblSpecError.Text = "This specialization already exists.";
                            lblSpecError.Visibility = Visibility.Visible;
                            return;
                        }
                    }

                    // Insert
                    string insertSql = "INSERT INTO Specializations (Name) VALUES (@name)";
                    using (var insertCmd = new SQLiteCommand(insertSql, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@name", specName);
                        insertCmd.ExecuteNonQuery();
                    }
                }

                txtSpecName.Text = string.Empty;
                LoadSpecializations();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error adding specialization: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteSpecialization_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Specialization spec)
            {
                var result = MessageBox.Show($"Are you sure you want to delete the specialization '{spec.Name}'?", 
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                        {
                            conn.Open();
                            string sql = "DELETE FROM Specializations WHERE SpecializationID = @id";
                            using (var cmd = new SQLiteCommand(sql, conn))
                            {
                                cmd.Parameters.AddWithValue("@id", spec.SpecializationID);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        LoadSpecializations();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error deleting specialization: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
