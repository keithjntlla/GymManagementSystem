using System;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace GymManagementSystem.Views.Windows
{
    public partial class AddUserWindow : Window
    {
        private ValidationHelper _validationHelper = null!;

        public AddUserWindow()
        {
            InitializeComponent();
            InitializeValidation();
        }

        private void InitializeValidation()
        {
            _validationHelper = new ValidationHelper();

            _validationHelper.RegisterTextBox(txtUsername, lblUsernameError, input =>
            {
                var (isValid, cleaned, error) = InputValidator.ValidateUsername(input);
                if (!isValid) return (false, cleaned, error);

                if (IsUsernameDuplicate(cleaned))
                {
                    return (false, cleaned, "Username is already taken by another account.");
                }
                return (true, cleaned, "");
            });

            _validationHelper.RegisterField(
                pbPassword, 
                lblPasswordError, 
                () => 
                {
                    string pwd = chkShowPassword.IsChecked == true ? txtPasswordVisible.Text : pbPassword.Password;
                    var (isValid, cleaned, error) = InputValidator.ValidatePassword(pwd);

                    // Sync both password border styles manually to reflect correct visual states
                    var brush = isValid ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51))
                                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68));
                    pbPassword.BorderBrush = brush;
                    txtPasswordVisible.BorderBrush = brush;

                    return (isValid, cleaned, error);
                }
            );

            // Trigger same validation check when txtPasswordVisible loses focus
            txtPasswordVisible.LostFocus += (s, e) =>
            {
                _validationHelper.ValidateAll();
            };
        }

        private bool IsUsernameDuplicate(string username)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT COUNT(*) FROM Users WHERE LOWER(Username) = LOWER(@user)";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@user", username);
                        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
                    }
                }
            }
            catch { return false; }
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!_validationHelper.ValidateAll())
            {
                return;
            }

            string username = txtUsername.Text.Trim();
            string password = chkShowPassword.IsChecked == true ? txtPasswordVisible.Text : pbPassword.Password;

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "INSERT INTO Users (UserID, Username, Password, Role, Status, CreatedDate) VALUES (@id, @user, @pass, @role, 'Active', @date)";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", "USR" + DateTime.Now.Ticks.ToString().Substring(12));
                        cmd.Parameters.AddWithValue("@user", username);
                        cmd.Parameters.AddWithValue("@pass", HashPassword(password));
                        cmd.Parameters.AddWithValue("@role", cbRole.Text);
                        cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd"));
                        cmd.ExecuteNonQuery();
                    }
                }
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex) { MessageBox.Show("Error saving user: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error); }
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
            _validationHelper.ValidateAll();
        }

        private void TogglePassword_Unchecked(object sender, RoutedEventArgs e)
        {
            pbPassword.Password = txtPasswordVisible.Text;
            txtPasswordVisible.Visibility = Visibility.Collapsed;
            pbPassword.Visibility = Visibility.Visible;
            _validationHelper.ValidateAll();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}