using System;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using GymManagementSystem.Models;
using GymManagementSystem.Views;

namespace GymManagementSystem.Views.Windows
{
    public partial class LoginWindow : Window
    {
        public static User? CurrentUser { get; private set; }

        private ValidationHelper _validationHelper = null!;

        public LoginWindow()
        {
            InitializeComponent();
            LoadBranding();
            InitializeValidation();
        }

        private void InitializeValidation()
        {
            _validationHelper = new ValidationHelper();

            _validationHelper.RegisterTextBox(txtUsername, lblUsernameError, input =>
            {
                if (string.IsNullOrWhiteSpace(input))
                {
                    return (false, "", "Username cannot be empty.");
                }
                return (true, input.Trim(), "");
            });

            _validationHelper.RegisterPasswordBox(txtPassword, lblPasswordError, input =>
            {
                if (string.IsNullOrEmpty(input))
                {
                    return (false, "", "Password cannot be empty.");
                }
                return (true, input, "");
            });
        }

        /// <summary>
        /// Loads gym name and logo from the database.
        /// Falls back to default logo and shows only "Staff Login" if nothing is set.
        /// </summary>
        private void LoadBranding()
        {
            var profile = DatabaseHelper.GetGymProfile();

            // Set gym name as the big title — hide it entirely if not set yet
            string gymName = profile.GetValueOrDefault("GymName", "");
            txtLoginTitle.Text = string.IsNullOrWhiteSpace(gymName) ? "Welcome!" : $"Welcome to {gymName}";
            txtLoginTitle.Visibility = Visibility.Visible;

            // Load custom logo if saved, otherwise the XAML default /Assets/logo.png stays
            string logoPath = profile.GetValueOrDefault("LogoPath", "");
            if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(logoPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    imgLogo.Source = bitmap;
                }
                catch
                {
                    // Corrupted custom logo — fall back to default
                    imgLogo.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/logo.png"));
                }
            }
            // No else needed — XAML already has Source="/Assets/logo.png" as default
        }

        private void btnSignIn_Click(object sender, RoutedEventArgs e)
        {
            if (!_validationHelper.ValidateAll())
            {
                return;
            }

            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;

            if (AuthenticateUser(username, password))
            {
                if (CurrentUser?.Status == "Inactive")
                {
                    MessageBox.Show("Your account has been deactivated. Please contact the Admin.", "Access Denied");
                    return;
                }

                if (CurrentUser?.MustChangePassword == true)
                {
                    this.Hide();

                    var changePassWin = new ChangePasswordWindow(CurrentUser.UserID);
                    bool? result = changePassWin.ShowDialog();

                    if (result != true)
                    {
                        this.Show();
                        MessageBox.Show("You must set a new password to continue.", "Password Change Required");
                        return;
                    }

                    CurrentUser.MustChangePassword = false;
                }

                MessageBox.Show($"Welcome back, {CurrentUser?.Username}!", "Login Success");
                OpenDashboard();
            }
            else
            {
                MessageBox.Show("Invalid username or password.", "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool AuthenticateUser(string username, string password)
        {
            try
            {
                string inputHashedPassword = HashPassword(password);

                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT UserID, Username, Role, Status, MustChangePassword FROM Users WHERE Username = @username AND Password = @password";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@password", inputHashedPassword);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                CurrentUser = new User
                                {
                                    UserID = reader["UserID"].ToString() ?? "",
                                    Username = reader["Username"].ToString() ?? "",
                                    Role = reader["Role"].ToString() ?? "",
                                    Status = reader["Status"].ToString() ?? "Active",
                                    MustChangePassword = Convert.ToInt32(reader["MustChangePassword"]) == 1
                                };
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database error during login: " + ex.Message);
            }
            return false;
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
        private void ForgotPassword_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MessageBox.Show("Please contact your administrator to reset your password.",
                            "Forgot Password", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenDashboard()
        {
            MainWindow main = new MainWindow();
            main.Show();
            this.Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}