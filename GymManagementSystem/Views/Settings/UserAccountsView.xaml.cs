using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using GymManagementSystem.Models;
using GymManagementSystem.Views.Windows;
using GymManagementSystem.Views.Settings;

namespace GymManagementSystem.Views.Settings
{
    public partial class UserAccountsView : UserControl
    {
        public UserAccountsView()
        {
            InitializeComponent();
            LoadUsers();
        }

        /// <summary>
        /// Fetches users from the database and refreshes the DataGrid.
        /// </summary>
        private void LoadUsers()
        {
            List<User> users = new List<User>();

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT UserID, Username, Role, Status FROM Users";

                    using (SQLiteCommand cmd = new SQLiteCommand(sql, conn))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new User
                            {
                                UserID = reader["UserID"].ToString() ?? "",
                                Username = reader["Username"].ToString() ?? "",
                                Role = reader["Role"].ToString() ?? "",
                                Status = reader["Status"].ToString() ?? "Active"
                            });
                        }
                    }
                }
                dgUsers.ItemsSource = users;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading users: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens the Add New User dialog.
        /// </summary>
        private void btnAddUser_Click(object sender, RoutedEventArgs e)
        {
            var addUserWin = new AddUserWindow();
            addUserWin.Owner = Window.GetWindow(this);
            if (addUserWin.ShowDialog() == true)
            {
                LoadUsers();
            }
        }

        /// <summary>
        /// Toggles a user's Active/Inactive status and saves it to the database.
        /// </summary>
        private void btnToggleStatus_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var user = button?.DataContext as User;

            if (user == null) return;

            // Prevent deactivating the currently logged-in admin
            if (user.UserID == LoginWindow.CurrentUser?.UserID)
            {
                MessageBox.Show("You cannot deactivate your own account.", "Action Not Allowed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string action = user.Status == "Active" ? "Deactivate" : "Activate";
            var result = MessageBox.Show($"Are you sure you want to {action} '{user.Username}'?",
                "Confirm Action", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                string newStatus = user.Status == "Active" ? "Inactive" : "Active";

                try
                {
                    using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        string sql = "UPDATE Users SET Status = @status WHERE UserID = @id";
                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@status", newStatus);
                            cmd.Parameters.AddWithValue("@id", user.UserID);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Refresh grid from DB to reflect real state
                    LoadUsers();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error updating user status: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Opens the Edit User Account dialog for the selected user.
        /// </summary>
        private void btnEditUser_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var user = button?.DataContext as User;

            if (user == null) return;

            var editUserWin = new EditUserWindow(user);
            editUserWin.Owner = Window.GetWindow(this);
            if (editUserWin.ShowDialog() == true)
            {
                LoadUsers();
            }
        }
    }
}