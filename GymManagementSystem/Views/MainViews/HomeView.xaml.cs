using GymManagementSystem.Models;
using GymManagementSystem.Views.Reports;
using GymManagementSystem.Views.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GymManagementSystem.Views.MainViews
{
    public class ExpiringMember
    {
        public string FullName { get; set; } = "";
        public string MemberID { get; set; } = "";
        public string Phone { get; set; } = "";
        public string ExpiryDate { get; set; } = "";
        public int DaysRemaining { get; set; }
        public string DaysLeft { get; set; } = "";
        public string? LastNotifiedDate { get; set; }
    }

    public partial class HomeView : UserControl
    {
        private Window? _ownerWindow;
        private List<ExpiringMember> _expiringMembers = new();

        public ObservableCollection<PaymentRecord> RecentTransactionsList { get; set; }
            = new ObservableCollection<PaymentRecord>();

        public HomeView()
        {
            InitializeComponent();
            DatabaseHelper.ProfileUpdated += RefreshHeader;
            Loaded += HomeView_Loaded;
            Unloaded += HomeView_Unloaded;
        }

        private void HomeView_Loaded(object sender, RoutedEventArgs e)
        {
            _ownerWindow = Window.GetWindow(this);
            if (_ownerWindow != null)
                _ownerWindow.Activated += OnOwnerWindowActivated;

            RefreshHeader();
            LoadDashboardData();
        }

        private void HomeView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_ownerWindow != null)
                _ownerWindow.Activated -= OnOwnerWindowActivated;
            _ownerWindow = null;
            DatabaseHelper.ProfileUpdated -= RefreshHeader;
        }

        private void OnOwnerWindowActivated(object? sender, EventArgs e)
        {
            if (IsVisible)
                LoadDashboardData();
        }

        private void RefreshHeader()
        {
            string username = LoginWindow.CurrentUser?.Username ?? "User";
            txtWelcomeMessage.Text = $"Welcome back, {username}! Here's what's happening today.";
        }

        private void LoadDashboardData()
        {
            DatabaseHelper.RefreshMemberStatuses();

            string today = DateTime.Today.ToString("yyyy-MM-dd");
            string through = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd");

            RecentTransactionsList.Clear();
            _expiringMembers.Clear();

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM Members WHERE Status = 'Active'", conn))
                        lblTotalMembers.Text = Convert.ToInt32(cmd.ExecuteScalar() ?? 0).ToString();

                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM Members WHERE Status = 'Expired'", conn))
                        lblExpiredMembers.Text = Convert.ToInt32(cmd.ExecuteScalar() ?? 0).ToString();

                    using (var cmd = new SQLiteCommand(
                        "SELECT COUNT(*) FROM Attendance WHERE CheckInDate = @today", conn))
                    {
                        cmd.Parameters.AddWithValue("@today", DateTime.Now.ToString("yyyy-MM-dd"));
                        lblTodayCheckIns.Text = Convert.ToInt32(cmd.ExecuteScalar() ?? 0).ToString();
                    }

                    using (var cmd = new SQLiteCommand(
                        "SELECT COUNT(*) FROM Attendance WHERE CheckInDate = @today AND (CheckOutTime IS NULL OR CheckOutTime = '')", conn))
                    {
                        cmd.Parameters.AddWithValue("@today", DateTime.Now.ToString("yyyy-MM-dd"));
                        lblCurrentlyIn.Text = Convert.ToInt32(cmd.ExecuteScalar() ?? 0).ToString();
                    }

                    using (var cmd = new SQLiteCommand(
                        @"SELECT SUM(TotalAmount)
                          FROM Payments
                          WHERE DateOfTransaction = @today
                            AND IFNULL(PaymentMode, '') <> 'Refund'
                            AND IFNULL(PaymentMode, '') <> 'Refunded'
                            AND IFNULL(MembershipType, '') NOT LIKE '[REFUND]%'
                            AND IFNULL(MembershipType, '') NOT LIKE '[REFUNDED]%'", conn))
                    {
                        cmd.Parameters.AddWithValue("@today", DateTime.Now.ToString("yyyy-MM-dd"));
                        object result = cmd.ExecuteScalar();
                        double revenue = result != null && result != DBNull.Value ? Convert.ToDouble(result) : 0;
                        lblTodayRevenue.Text = $"₱{revenue:N2}";
                    }

                    using (var cmd = new SQLiteCommand(
                        @"SELECT SUM(TotalAmount)
                          FROM Payments
                          WHERE strftime('%Y-%m', DateOfTransaction) = @month
                            AND IFNULL(PaymentMode, '') <> 'Refund'
                            AND IFNULL(PaymentMode, '') <> 'Refunded'
                            AND IFNULL(MembershipType, '') NOT LIKE '[REFUND]%'
                            AND IFNULL(MembershipType, '') NOT LIKE '[REFUNDED]%'", conn))
                    {
                        cmd.Parameters.AddWithValue("@month", DateTime.Now.ToString("yyyy-MM"));
                        object result = cmd.ExecuteScalar();
                        double revenue = result != null && result != DBNull.Value ? Convert.ToDouble(result) : 0;
                        lblMonthlyRevenue.Text = $"₱{revenue:N2}";
                    }

                    using (var cmd = new SQLiteCommand(
                        "SELECT COUNT(*) FROM Members WHERE strftime('%Y-%m', DateJoined) = @month", conn))
                    {
                        cmd.Parameters.AddWithValue("@month", DateTime.Now.ToString("yyyy-MM"));
                        lblNewMembers.Text = Convert.ToInt32(cmd.ExecuteScalar() ?? 0).ToString();
                    }

                    string expiringFilter = @"Status = 'Active' AND ExpiryDate != '-' AND ExpiryDate != ''
                        AND DATE(ExpiryDate) >= DATE(@today) AND DATE(ExpiryDate) <= DATE(@through)";

                    using (var cmd = new SQLiteCommand($"SELECT COUNT(*) FROM Members WHERE {expiringFilter}", conn))
                    {
                        cmd.Parameters.AddWithValue("@today", today);
                        cmd.Parameters.AddWithValue("@through", through);
                        lblExpiringSubscriptions.Text = Convert.ToInt32(cmd.ExecuteScalar() ?? 0).ToString();
                    }

                    using (var cmd = new SQLiteCommand($@"SELECT MemberID, FullName, Phone, ExpiryDate
                        FROM Members WHERE {expiringFilter}
                        ORDER BY ExpiryDate ASC", conn))
                    {
                        cmd.Parameters.AddWithValue("@today", today);
                        cmd.Parameters.AddWithValue("@through", through);

                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string expiryStr = reader["ExpiryDate"]?.ToString() ?? "";
                            int days = ExpirationUiHelper.CalcDaysRemaining(expiryStr);
                            if (days < 0)
                                continue;

                            _expiringMembers.Add(new ExpiringMember
                            {
                                MemberID = reader["MemberID"]?.ToString() ?? "",
                                FullName = reader["FullName"]?.ToString() ?? "",
                                Phone = reader["Phone"]?.ToString() ?? "",
                                ExpiryDate = expiryStr,
                                DaysRemaining = days,
                                DaysLeft = days == 0 ? "Today" : $"{days}d left"
                            });
                        }
                    }

                    var notifiedDates = NotificationHelper.GetLatestNotifiedDates(
                        _expiringMembers.Select(m => m.MemberID));

                    foreach (var member in _expiringMembers)
                    {
                        notifiedDates.TryGetValue(member.MemberID, out string? lastDate);
                        member.LastNotifiedDate = lastDate;
                    }

                    dgExpiringSoon.ItemsSource = _expiringMembers.ToList();
                    ExpiringSoonSection.Visibility = Visibility.Visible;

                    using (var cmd = new SQLiteCommand(@"SELECT MemberName, MembershipType, TotalAmount, AmountPaid, DateOfTransaction, PaymentMode
                        FROM Payments
                        WHERE IFNULL(PaymentMode, '') <> 'Refunded'
                          AND IFNULL(MembershipType, '') NOT LIKE '[REFUNDED]%'
                        ORDER BY PaymentID DESC LIMIT 5", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            double netCost = reader["TotalAmount"] != DBNull.Value
                                ? Convert.ToDouble(reader["TotalAmount"])
                                : Convert.ToDouble(reader["AmountPaid"] ?? 0);

                            RecentTransactionsList.Add(new PaymentRecord
                            {
                                MemberName = reader["MemberName"]?.ToString() ?? "",
                                MembershipType = reader["MembershipType"]?.ToString() ?? "",
                                TotalAmount = netCost,
                                DateOfTransaction = reader["DateOfTransaction"]?.ToString() ?? "",
                                PaymentMode = reader["PaymentMode"]?.ToString() ?? ""
                            });
                        }
                    }
                    dgRecentTransactions.ItemsSource = RecentTransactionsList;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading dashboard data: " + ex.Message);
            }
        }

        private void ViewExpirationReport_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow == null)
                return;

            var reportsView = new ReportsView();
            reportsView.ShowExpirationsTab();
            mainWindow.MainFrame.Content = reportsView;
            mainWindow.btnNavReports.IsChecked = true;
        }

        private void PayMember_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is ExpiringMember member)
                PaymentNavigationHelper.TryNavigateToPayment(this, member.MemberID);
        }

        private void NotifyMember_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is not ExpiringMember member)
                return;

            if (NotificationHelper.WasNotifiedOnDate(member.LastNotifiedDate, NotificationHelper.Today))
            {
                MessageBox.Show(
                    $"{member.FullName} has already been notified today. You can notify this member again tomorrow.",
                    "Already Notified",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var items = new List<NotificationMemberItem>
            {
                new NotificationMemberItem
                {
                    MemberID = member.MemberID,
                    FullName = member.FullName,
                    Phone = member.Phone,
                    ExpiryDate = ExpirationUiHelper.FormatExpiryDate(member.ExpiryDate),
                    DaysRemaining = member.DaysRemaining,
                    DaysRemainingLabel = ExpirationUiHelper.BuildDaysLabel(member.DaysRemaining),
                    UrgencyLevel = ExpirationUiHelper.ClassifyUrgency(member.DaysRemaining),
                    LastNotifiedDate = member.LastNotifiedDate,
                    NotifiedStatusLabel = NotificationHelper.BuildNotifiedStatusLabel(member.LastNotifiedDate),
                    IsSelected = true
                }
            };

            var dialog = new ReviewNotificationsWindow(items)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                MessageBox.Show($"Successfully notified {dialog.NotifiedCount} members.",
                    "Notifications Sent", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadDashboardData();
            }
        }
    }
}
