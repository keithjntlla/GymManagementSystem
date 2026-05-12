using System;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace GymManagementSystem.Views.Windows
{
    public partial class ChangePasswordWindow : Window
    {
        private readonly string _userId;

        public ChangePasswordWindow(string userId)
        {
            InitializeComponent();
            _userId = userId;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // Get values from whichever field is currently visible
            string newPassword = chkShowNew.IsChecked == true
                ? txtNewVisible.Text
                : pbNewPassword.Password;

            string confirmPassword = chkShowConfirm.IsChecked == true
                ? txtConfirmVisible.Text
                : pbConfirm.Password;

            // Validate
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                ShowError("Please enter a new password.");
                return;
            }

            if (newPassword.Length < 6)
            {
                ShowError("Password must be at least 6 characters long.");
                return;
            }

            if (newPassword != confirmPassword)
            {
                ShowError("Passwords do not match. Please try again.");
                return;
            }

            // Save to DB
            try
            {
                string hashed = HashPassword(newPassword);

                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "UPDATE Users SET Password = @pass, MustChangePassword = 0 WHERE UserID = @id";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@pass", hashed);
                        cmd.Parameters.AddWithValue("@id", _userId);
                        cmd.ExecuteNonQuery();
                    }
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                ShowError("Database error: " + ex.Message);
            }
        }

        private void ShowError(string message)
        {
            lblError.Text = message;
            lblError.Visibility = Visibility.Visible;
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        // Toggle New Password visibility
        private void chkShowNew_Checked(object sender, RoutedEventArgs e)
        {
            txtNewVisible.Text = pbNewPassword.Password;
            pbNewPassword.Visibility = Visibility.Collapsed;
            txtNewVisible.Visibility = Visibility.Visible;
        }

        private void chkShowNew_Unchecked(object sender, RoutedEventArgs e)
        {
            pbNewPassword.Password = txtNewVisible.Text;
            txtNewVisible.Visibility = Visibility.Collapsed;
            pbNewPassword.Visibility = Visibility.Visible;
        }

        // Toggle Confirm Password visibility
        private void chkShowConfirm_Checked(object sender, RoutedEventArgs e)
        {
            txtConfirmVisible.Text = pbConfirm.Password;
            pbConfirm.Visibility = Visibility.Collapsed;
            txtConfirmVisible.Visibility = Visibility.Visible;
        }

        private void chkShowConfirm_Unchecked(object sender, RoutedEventArgs e)
        {
            pbConfirm.Password = txtConfirmVisible.Text;
            txtConfirmVisible.Visibility = Visibility.Collapsed;
            pbConfirm.Visibility = Visibility.Visible;
        }
    }
}