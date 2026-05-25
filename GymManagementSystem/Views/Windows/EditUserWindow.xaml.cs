using System;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GymManagementSystem.Models;

namespace GymManagementSystem.Views.Windows
{
    public partial class EditUserWindow : Window
    {
        private readonly User _user;
        private readonly bool _isSelfEdit;
        private ValidationHelper _validationHelper = null!;

        public EditUserWindow(User userToEdit)
        {
            InitializeComponent();
            _user = userToEdit;
            _isSelfEdit = _user.UserID == LoginWindow.CurrentUser?.UserID;

            LoadUserData();
            InitializeValidation();
        }

        private void LoadUserData()
        {
            txtUsername.Text = _user.Username;
            
            // Set role selection
            foreach (ComboBoxItem item in cbRole.Items)
            {
                if (item.Content?.ToString() == _user.Role)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            secCurrentPassword.Visibility = Visibility.Visible;
            if (_isSelfEdit)
            {
                cbRole.IsEnabled = false; // Block self-demotion
            }
            else
            {
                cbRole.IsEnabled = true;
            }
        }

        private void InitializeValidation()
        {
            _validationHelper = new ValidationHelper();

            // Username validation
            _validationHelper.RegisterTextBox(txtUsername, lblUsernameError, input =>
            {
                var (isValid, cleaned, error) = InputValidator.ValidateUsername(input);
                if (!isValid) return (false, cleaned, error);

                if (IsUsernameDuplicate(cleaned, _user.UserID))
                {
                    return (false, cleaned, "Username is already taken by another account.");
                }
                return (true, cleaned, "");
            });

            // Current password validation (always active/checked)
            _validationHelper.RegisterField(
                pbCurrentPassword,
                lblCurrentPasswordError,
                () =>
                {
                    string currentPwd = chkShowPasswords.IsChecked == true ? txtCurrentPasswordVisible.Text : pbCurrentPassword.Password;
                    if (string.IsNullOrEmpty(currentPwd))
                    {
                        var redBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                        pbCurrentPassword.BorderBrush = redBrush;
                        txtCurrentPasswordVisible.BorderBrush = redBrush;
                        return (false, "", "Current password is required to save changes.");
                    }

                    // Validate against database
                    if (!VerifyCurrentPassword(currentPwd))
                    {
                        var redBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                        pbCurrentPassword.BorderBrush = redBrush;
                        txtCurrentPasswordVisible.BorderBrush = redBrush;
                        return (false, "", "Incorrect current password. Please try again.");
                    }

                    var grayBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                    pbCurrentPassword.BorderBrush = grayBrush;
                    txtCurrentPasswordVisible.BorderBrush = grayBrush;
                    return (true, currentPwd, "");
                }
            );

            txtCurrentPasswordVisible.LostFocus += (s, e) => { _validationHelper.ValidateAll(); };

            // New password validation (optional)
            _validationHelper.RegisterField(
                pbNewPassword,
                lblNewPasswordError,
                () =>
                {
                    string newPwd = chkShowPasswords.IsChecked == true ? txtNewPasswordVisible.Text : pbNewPassword.Password;
                    if (string.IsNullOrEmpty(newPwd))
                    {
                        // Optional - blank is valid
                        var grayBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                        pbNewPassword.BorderBrush = grayBrush;
                        txtNewPasswordVisible.BorderBrush = grayBrush;
                        return (true, "", "");
                    }

                    string currentPwd = chkShowPasswords.IsChecked == true ? txtCurrentPasswordVisible.Text : pbCurrentPassword.Password;
                    if (newPwd == currentPwd)
                    {
                        var redBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                        pbNewPassword.BorderBrush = redBrush;
                        txtNewPasswordVisible.BorderBrush = redBrush;
                        return (false, "", "New password cannot be the same as your old password.");
                    }

                    var (isValid, cleaned, error) = InputValidator.ValidatePassword(newPwd);
                    var borderBrush = isValid ? new SolidColorBrush(Color.FromRgb(51, 51, 51))
                                               : new SolidColorBrush(Color.FromRgb(255, 68, 68));
                    pbNewPassword.BorderBrush = borderBrush;
                    txtNewPasswordVisible.BorderBrush = borderBrush;

                    return (isValid, cleaned, error);
                }
            );

            // Confirm new password validation (optional, must match new password)
            _validationHelper.RegisterField(
                pbConfirmNewPassword,
                lblConfirmNewPasswordError,
                () =>
                {
                    string newPwd = chkShowPasswords.IsChecked == true ? txtNewPasswordVisible.Text : pbNewPassword.Password;
                    string confirmPwd = chkShowPasswords.IsChecked == true ? txtConfirmNewPasswordVisible.Text : pbConfirmNewPassword.Password;

                    if (string.IsNullOrEmpty(newPwd) && string.IsNullOrEmpty(confirmPwd))
                    {
                        var grayBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                        pbConfirmNewPassword.BorderBrush = grayBrush;
                        txtConfirmNewPasswordVisible.BorderBrush = grayBrush;
                        return (true, "", "");
                    }

                    if (newPwd != confirmPwd)
                    {
                        var redBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                        pbConfirmNewPassword.BorderBrush = redBrush;
                        txtConfirmNewPasswordVisible.BorderBrush = redBrush;
                        return (false, "", "Passwords do not match.");
                    }

                    var borderBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                    pbConfirmNewPassword.BorderBrush = borderBrush;
                    txtConfirmNewPasswordVisible.BorderBrush = borderBrush;
                    return (true, confirmPwd, "");
                }
            );

            txtNewPasswordVisible.LostFocus += (s, e) => { _validationHelper.ValidateAll(); };
            txtConfirmNewPasswordVisible.LostFocus += (s, e) => { _validationHelper.ValidateAll(); };
        }

        private bool IsUsernameDuplicate(string username, string excludeUserId)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT COUNT(*) FROM Users WHERE LOWER(Username) = LOWER(@user) AND UserID != @exclude";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@user", username);
                        cmd.Parameters.AddWithValue("@exclude", excludeUserId);
                        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
                    }
                }
            }
            catch { return false; }
        }

        private bool VerifyCurrentPassword(string password)
        {
            try
            {
                string hashed = InputValidator.HashPassword(password);
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT Password FROM Users WHERE UserID = @id";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", _user.UserID);
                        string savedHash = cmd.ExecuteScalar()?.ToString() ?? "";
                        return hashed == savedHash;
                    }
                }
            }
            catch { return false; }
        }

        private void TogglePassword_Checked(object sender, RoutedEventArgs e)
        {
            // Sync pb to txt and swap visibility
            txtCurrentPasswordVisible.Text = pbCurrentPassword.Password;
            pbCurrentPassword.Visibility = Visibility.Collapsed;
            txtCurrentPasswordVisible.Visibility = Visibility.Visible;

            txtNewPasswordVisible.Text = pbNewPassword.Password;
            pbNewPassword.Visibility = Visibility.Collapsed;
            txtNewPasswordVisible.Visibility = Visibility.Visible;

            txtConfirmNewPasswordVisible.Text = pbConfirmNewPassword.Password;
            pbConfirmNewPassword.Visibility = Visibility.Collapsed;
            txtConfirmNewPasswordVisible.Visibility = Visibility.Visible;

            _validationHelper.ValidateAll();
        }

        private void TogglePassword_Unchecked(object sender, RoutedEventArgs e)
        {
            // Sync txt to pb and swap visibility
            pbCurrentPassword.Password = txtCurrentPasswordVisible.Text;
            txtCurrentPasswordVisible.Visibility = Visibility.Collapsed;
            pbCurrentPassword.Visibility = Visibility.Visible;

            pbNewPassword.Password = txtNewPasswordVisible.Text;
            txtNewPasswordVisible.Visibility = Visibility.Collapsed;
            pbNewPassword.Visibility = Visibility.Visible;

            pbConfirmNewPassword.Password = txtConfirmNewPasswordVisible.Text;
            txtConfirmNewPasswordVisible.Visibility = Visibility.Collapsed;
            pbConfirmNewPassword.Visibility = Visibility.Visible;

            _validationHelper.ValidateAll();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!_validationHelper.ValidateAll()) return;

            string cleanedUsername = txtUsername.Text.Trim();
            string selectedRole = cbRole.Text;
            string newPassword = chkShowPasswords.IsChecked == true ? txtNewPasswordVisible.Text : pbNewPassword.Password;

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql;
                    if (!string.IsNullOrEmpty(newPassword))
                    {
                        // Modifying username, role, and password
                        sql = "UPDATE Users SET Username = @user, Role = @role, Password = @pass WHERE UserID = @id";
                    }
                    else
                    {
                        // Modifying username and role only
                        sql = "UPDATE Users SET Username = @user, Role = @role WHERE UserID = @id";
                    }

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@user", cleanedUsername);
                        cmd.Parameters.AddWithValue("@role", selectedRole);
                        cmd.Parameters.AddWithValue("@id", _user.UserID);
                        if (!string.IsNullOrEmpty(newPassword))
                        {
                            cmd.Parameters.AddWithValue("@pass", InputValidator.HashPassword(newPassword));
                        }
                        cmd.ExecuteNonQuery();
                    }
                }

                // If self-edited, sync session User in memory
                if (_isSelfEdit && LoginWindow.CurrentUser != null)
                {
                    LoginWindow.CurrentUser.Username = cleanedUsername;
                    // Note: Role cannot change due to demotion guard, but let's sync in case
                    LoginWindow.CurrentUser.Role = selectedRole;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error updating user details: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
