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
        private ValidationHelper _validationHelper = null!;

        public ChangePasswordWindow(string userId)
        {
            InitializeComponent();
            _userId = userId;
            InitializeValidation();
        }

        private void InitializeValidation()
        {
            _validationHelper = new ValidationHelper();

            _validationHelper.RegisterField(
                pbNewPassword, 
                lblNewPasswordError, 
                () => 
                {
                    string pwd = chkShowNew.IsChecked == true ? txtNewVisible.Text : pbNewPassword.Password;
                    var (isValid, cleaned, error) = InputValidator.ValidatePassword(pwd);

                    var brush = isValid ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 68))
                                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68));
                    pbNewPassword.BorderBrush = brush;
                    txtNewVisible.BorderBrush = brush;

                    return (isValid, cleaned, error);
                }
            );

            _validationHelper.RegisterField(
                pbConfirm, 
                lblConfirmPasswordError, 
                () => 
                {
                    string confirmPwd = chkShowConfirm.IsChecked == true ? txtConfirmVisible.Text : pbConfirm.Password;
                    string newPwd = chkShowNew.IsChecked == true ? txtNewVisible.Text : pbNewPassword.Password;

                    if (string.IsNullOrEmpty(confirmPwd))
                    {
                        var errorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68));
                        pbConfirm.BorderBrush = errorBrush;
                        txtConfirmVisible.BorderBrush = errorBrush;
                        return (false, "", "Confirm Password cannot be empty.");
                    }

                    if (newPwd != confirmPwd)
                    {
                        var errorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68));
                        pbConfirm.BorderBrush = errorBrush;
                        txtConfirmVisible.BorderBrush = errorBrush;
                        return (false, "", "Passwords do not match. Please try again.");
                    }

                    var normalBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 68));
                    pbConfirm.BorderBrush = normalBrush;
                    txtConfirmVisible.BorderBrush = normalBrush;

                    return (true, confirmPwd, "");
                }
            );

            txtNewVisible.LostFocus += (s, e) => _validationHelper.ValidateAll();
            txtConfirmVisible.LostFocus += (s, e) => _validationHelper.ValidateAll();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!_validationHelper.ValidateAll())
            {
                return;
            }

            string newPassword = chkShowNew.IsChecked == true
                ? txtNewVisible.Text
                : pbNewPassword.Password;

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
                MessageBox.Show("Error updating password: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            _validationHelper.ValidateAll();
        }

        private void chkShowNew_Unchecked(object sender, RoutedEventArgs e)
        {
            pbNewPassword.Password = txtNewVisible.Text;
            txtNewVisible.Visibility = Visibility.Collapsed;
            pbNewPassword.Visibility = Visibility.Visible;
            _validationHelper.ValidateAll();
        }

        // Toggle Confirm Password visibility
        private void chkShowConfirm_Checked(object sender, RoutedEventArgs e)
        {
            txtConfirmVisible.Text = pbConfirm.Password;
            pbConfirm.Visibility = Visibility.Collapsed;
            txtConfirmVisible.Visibility = Visibility.Visible;
            _validationHelper.ValidateAll();
        }

        private void chkShowConfirm_Unchecked(object sender, RoutedEventArgs e)
        {
            pbConfirm.Password = txtConfirmVisible.Text;
            txtConfirmVisible.Visibility = Visibility.Collapsed;
            pbConfirm.Visibility = Visibility.Visible;
            _validationHelper.ValidateAll();
        }
    }
}