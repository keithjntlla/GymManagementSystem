using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using GymManagementSystem.Views.MainViews;
using GymManagementSystem.Views.Reports;
using GymManagementSystem.Views.Settings;

namespace GymManagementSystem.Views.Windows
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            WindowState = WindowState.Maximized;
            DatabaseHelper.ProfileUpdated += RefreshBranding;
            RefreshBranding();
            // Load the dashboard by default
            MainFrame.Content = new HomeView();
        }

        // In MainWindow.xaml.cs
        private void RefreshBranding()
        {
            var profile = DatabaseHelper.GetGymProfile();

            // Update Gym Name text
            string gymName = profile.GetValueOrDefault("GymName", "Gym Name");
            txtSidebarGymName.Text = $"{gymName}";

            // Update Sidebar Logo instantly
            string logoPath = profile.GetValueOrDefault("LogoPath", "");
            if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
            {
                try
                {
                    // Use BitmapCacheOption.OnLoad to prevent file locking issues
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(logoPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    imgSidebarLogo.Source = bitmap;
                }
                catch
                {
                    // Fallback if image is corrupted
                    imgSidebarLogo.Source = new BitmapImage(new Uri("/Assets/logo.png", UriKind.Relative));
                }
            }
            else
            {
                // Use default logo if no path is saved
                imgSidebarLogo.Source = new BitmapImage(new Uri("/Assets/logo.png", UriKind.Relative));
            }
        }

        private void NavHome_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Content = new HomeView();
        }

        private void NavMembers_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Content = new MembersView();
        }

        private void NavInstructors_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Content = new InstructorsView();
        }

        private void NavPayments_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Content = new PaymentsView();
        }

        private void NavAttendance_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Content = new AttendanceView();
        }

        private void NavReports_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Content = new ReportsView();
        }

        private bool _settingsExpanded = false;

        private void NavSettingsToggle_Click(object sender, RoutedEventArgs e)
        {
            if (LoginWindow.CurrentUser?.Role != "Administrator")
            {
                MessageBox.Show("You do not have permission to access Settings.",
                                "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _settingsExpanded = !_settingsExpanded;
            SettingsSubMenu.Visibility = _settingsExpanded
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private void NavServicesPricing_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Content = new SettingsView();
        }

        private void NavSystem_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Content = new SystemSettingsView();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to log out?",
                                 "Confirm Logout", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                LoginWindow login = new LoginWindow();
                login.Show();
                this.Close();
            }
        }
    }
}
