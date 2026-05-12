using Microsoft.Win32;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using GymManagementSystem.Views.Settings;

namespace GymManagementSystem.Views.Settings
{
    public partial class BackupSettingsView : UserControl
    {
        private static readonly string DbPath =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GymManagement.db");

        public BackupSettingsView()
        {
            InitializeComponent();
        }

        private void btnBackup_Click(object sender, RoutedEventArgs e)
        {
            // Let the user choose where to save the backup
            var dialog = new SaveFileDialog
            {
                Title = "Save Database Backup",
                FileName = $"GymManagement_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm}",
                DefaultExt = ".db",
                Filter = "SQLite Database|*.db"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                // Use SQLite's built-in backup API for a safe, consistent copy
                using (var source = new SQLiteConnection(DatabaseHelper.ConnectionString))
                using (var dest = new SQLiteConnection($"Data Source={dialog.FileName};Version=3;"))
                {
                    source.Open();
                    dest.Open();
                    source.BackupDatabase(dest, "main", "main", -1, null, 0);
                }

                MessageBox.Show($"Backup saved successfully to:\n{dialog.FileName}",
                    "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Backup failed: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnRestore_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user before proceeding
            var confirm = MessageBox.Show(
                "Restoring a backup will replace ALL current data and the app will close.\n\n" +
                "Make sure you have a backup of your current data before continuing.\n\n" +
                "Do you want to proceed?",
                "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            // Let the user pick the backup file
            var dialog = new OpenFileDialog
            {
                Title = "Select Backup File",
                DefaultExt = ".db",
                Filter = "SQLite Database|*.db"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                // Close all connections before replacing the file
                SQLiteConnection.ClearAllPools();

                // Replace current DB with backup
                File.Copy(dialog.FileName, DbPath, overwrite: true);

                MessageBox.Show(
                    "Database restored successfully.\n\nThe application will now close. Please reopen it.",
                    "Restore Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                // Shut down the app so it restarts cleanly with the restored DB
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Restore failed: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}