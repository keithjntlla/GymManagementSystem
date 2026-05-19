using System;
using System.Collections.Generic;
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
using GymManagementSystem.Views.Reports;
using GymManagementSystem.Views.Settings;

namespace GymManagementSystem.Views.Settings
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            // Load General tab by default
            SettingsFrame.Content = new GeneralSettingsView();
        }

        private void TabRates_Click(object sender, RoutedEventArgs e)
        {
            SettingsFrame.Content = new RateSettingsView();
        }

        private void TabDiscounts_Click(object sender, RoutedEventArgs e)
        {
            SettingsFrame.Content = new DiscountSettingsView();
        }

        private void TabGeneral_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder for General Settings
            SettingsFrame.Content = new GeneralSettingsView();
        }

        private void TabUsers_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder for User Accounts
            SettingsFrame.Content = new UserAccountsView();
        }

        private void TabBackup_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder for Backup & Restore
            SettingsFrame.Content = new BackupSettingsView();
        }
    }
}