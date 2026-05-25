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

namespace GymManagementSystem.Views.Settings
{
    /// <summary>
    /// Interaction logic for SystemSettingsView.xaml
    /// </summary>
    public partial class SystemSettingsView : UserControl
    {
        public SystemSettingsView()
        {
            InitializeComponent();
            // Load General tab by default
            SettingsFrame.Content = new GeneralSettingsView();
        }

        private void TabGeneral_Click(object sender, RoutedEventArgs e)
        {
            SettingsFrame.Content = new GeneralSettingsView();
        }

        private void TabUsers_Click(object sender, RoutedEventArgs e)
        {
            SettingsFrame.Content = new UserAccountsView();
        }

        private void TabBackup_Click(object sender, RoutedEventArgs e)
        {
            SettingsFrame.Content = new BackupSettingsView();
        }

        private void TabAppearance_Click(object sender, RoutedEventArgs e)
        {
            SettingsFrame.Content = new AppearanceSettingsView();
        }
    }
}
