using GymManagementSystem.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using GymManagementSystem.Views.Reports;

namespace GymManagementSystem.Views.Reports;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        ReportsFrame.Content = new FinancialReportsView();
    }
    private void TabFinancial_Click(object sender, RoutedEventArgs e)
    {
        ReportsFrame.Content = new FinancialReportsView();
    }

    private void TabAttendance_Click(object sender, RoutedEventArgs e)
    {
        ReportsFrame.Content = new AttendanceReportsView();
    }

    private void TabExpirations_Click(object sender, RoutedEventArgs e)
    {
        ReportsFrame.Content = new ExpirationReportsView();
    }

    public void ShowExpirationsTab()
    {
        rbTabExpirations.IsChecked = true;
        ReportsFrame.Content = new ExpirationReportsView();
    }

    public void ShowFinancialTab()
    {
        rbTabFinancial.IsChecked = true;
        ReportsFrame.Content = new FinancialReportsView();
    }

    private void TabRefunds_Click(object sender, RoutedEventArgs e)
    {
        ReportsFrame.Content = new RefundReportsView();
    }

    private void TabDiscounts_Click(object sender, RoutedEventArgs e)
    {
        ReportsFrame.Content = new DiscountReportsView();
    }
}
