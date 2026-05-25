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
    /// Interaction logic for SettingsView.xaml (Services &amp; Pricing)
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            // Load Rates & Plans tab by default
            SettingsFrame.Content = new RateSettingsView();
        }

        private void TabRates_Click(object sender, RoutedEventArgs e)
        {
            SettingsFrame.Content = new RateSettingsView();
        }

        private void TabDiscounts_Click(object sender, RoutedEventArgs e)
        {
            SettingsFrame.Content = new DiscountSettingsView();
        }

        private void TabPromos_Click(object sender, RoutedEventArgs e)
        {
            SettingsFrame.Content = new PromosSettingsView();
        }

        private void TabSpecializations_Click(object sender, RoutedEventArgs e)
        {
            SettingsFrame.Content = new SpecializationSettingsView();
        }
    }
}