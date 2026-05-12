using System;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace GymManagementSystem.Views.Windows
{
    public partial class AddUserWindow : Window
    {
        public AddUserWindow() => InitializeComponent();

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            string password = chkShowPassword.IsChecked == true ? txtPasswordVisible.Text : pbPassword.Password;

            if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("All fields are required.");
                return;
            }

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "INSERT INTO Users (UserID, Username, Password, Role, Status, CreatedDate) VALUES (@id, @user, @pass, @role, 'Active', @date)";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", "USR" + DateTime.Now.Ticks.ToString().Substring(12));
                        cmd.Parameters.AddWithValue("@user", txtUsername.Text.Trim());
                        cmd.Parameters.AddWithValue("@pass", HashPassword(password));
                        cmd.Parameters.AddWithValue("@role", cbRole.Text);
                        cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd"));
                        cmd.ExecuteNonQuery();
                    }
                }
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private string HashPassword(string pass)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(pass));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private void TogglePassword_Checked(object sender, RoutedEventArgs e)
        {
            txtPasswordVisible.Text = pbPassword.Password;
            pbPassword.Visibility = Visibility.Collapsed;
            txtPasswordVisible.Visibility = Visibility.Visible;
        }

        private void TogglePassword_Unchecked(object sender, RoutedEventArgs e)
        {
            pbPassword.Password = txtPasswordVisible.Text;
            txtPasswordVisible.Visibility = Visibility.Collapsed;
            pbPassword.Visibility = Visibility.Visible;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}