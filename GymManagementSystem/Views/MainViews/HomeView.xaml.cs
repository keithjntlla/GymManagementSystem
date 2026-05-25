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
using System.Windows.Threading;

namespace GymManagementSystem.Views.MainViews
{
    public partial class HomeView : UserControl
    {
        private Window? _ownerWindow;
        private List<ExpiringMember> _expiringMembers = new();
        private string currentInlineSelectedMemberId = "";
        private bool isSelectingSuggestion = false;
        private DispatcherTimer? _inlineAlertTimer;

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

            // Checked in/out and other states are loaded as standard

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

                    // Load Gym Profile and Max Capacity
                    var profile = DatabaseHelper.GetGymProfile();
                    lblMaxCapacity.Text = profile.GetValueOrDefault("MaxCapacity", "100");

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

                    // Load Pending Registrations
                    List<Member> pendingList = new List<Member>();
                    using (var cmd = new SQLiteCommand(
                        "SELECT MemberID, FirstName, MiddleInitial, LastName, DateJoined, Status FROM Members WHERE Status = 'Pending' ORDER BY DateJoined DESC", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            pendingList.Add(new Member
                            {
                                MemberID = reader["MemberID"]?.ToString() ?? "",
                                FirstName = reader["FirstName"]?.ToString() ?? "",
                                MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                                LastName = reader["LastName"]?.ToString() ?? "",
                                DateJoined = reader["DateJoined"]?.ToString() ?? "",
                                Status = reader["Status"]?.ToString() ?? ""
                            });
                        }
                    }
                    dgPendingRegistrations.ItemsSource = pendingList;
                    lblPendingRegistrationsCount.Text = pendingList.Count.ToString();

                    // Load Recent Transactions
                    string sevenDaysAgo = DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd");
                    using (var cmd = new SQLiteCommand(@"
                        SELECT P.PaymentID, P.MemberID, P.MemberName, P.MembershipType, P.TotalAmount, P.AmountPaid, 
                               P.DateOfTransaction, P.PaymentMode, P.DiscountAmount,
                               COALESCE(M.MemberType, 'Regular') AS MemberType, MP.PromoCode
                        FROM Payments P
                        LEFT JOIN Members M ON P.MemberID = M.MemberID
                        LEFT JOIN MemberPromos MP ON P.PaymentID = MP.PaymentID
                        WHERE IFNULL(P.PaymentMode, '') <> 'Refunded'
                          AND IFNULL(P.MembershipType, '') NOT LIKE '[REFUNDED]%'
                          AND Date(P.DateOfTransaction) >= Date(@sevenDaysAgo)
                        ORDER BY P.PaymentID DESC LIMIT 10", conn))
                    {
                        cmd.Parameters.AddWithValue("@sevenDaysAgo", sevenDaysAgo);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                double netCost = reader["TotalAmount"] != DBNull.Value
                                    ? Convert.ToDouble(reader["TotalAmount"])
                                    : Convert.ToDouble(reader["AmountPaid"] ?? 0);

                                string rawDate = reader["DateOfTransaction"]?.ToString() ?? "";
                                string formattedDate = rawDate;
                                if (DateTime.TryParse(rawDate, out DateTime parsedTxDate))
                                {
                                    formattedDate = parsedTxDate.ToString("MM-dd-yyyy");
                                }

                                RecentTransactionsList.Add(new PaymentRecord
                                {
                                    MemberName = reader["MemberName"]?.ToString() ?? "",
                                    MembershipType = reader["MembershipType"]?.ToString() ?? "",
                                    TotalAmount = netCost,
                                    DateOfTransaction = formattedDate,
                                    PaymentMode = reader["PaymentMode"]?.ToString() ?? "",
                                    DiscountAmount = reader["DiscountAmount"] != DBNull.Value ? Convert.ToDouble(reader["DiscountAmount"]) : 0,
                                    PromoCode = reader["PromoCode"]?.ToString() ?? string.Empty,
                                    MemberType = reader["MemberType"]?.ToString() ?? "Regular"
                                });
                            }
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

        private void CollectPendingPayment_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is Member member)
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

        // Admin capacity edit button is disabled from dashboard

        // Inline Check-in / Out Logic
        private void TxtInlineSearch_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                string input = txtInlineSearch.Text.Trim();
                if (!string.IsNullOrEmpty(input))
                {
                    popInlineSearch.IsOpen = false;
                    ProcessInlineCheck(input);
                }
                e.Handled = true;
            }
        }

        private void TxtInlineSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isSelectingSuggestion) return;

            string query = txtInlineSearch.Text.Trim();
            if (query.Length >= 1)
            {
                List<Member> suggestions = new List<Member>();
                try
                {
                    using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        string sql = "SELECT MemberID, FirstName, MiddleInitial, LastName FROM Members WHERE FullName LIKE @q OR MemberID LIKE @q LIMIT 10";
                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@q", "%" + query + "%");
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    suggestions.Add(new Member
                                    {
                                        MemberID = reader["MemberID"]?.ToString() ?? "",
                                        FirstName = reader["FirstName"]?.ToString() ?? "",
                                        MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                                        LastName = reader["LastName"]?.ToString() ?? ""
                                    });
                                }
                            }
                        }
                    }
                    lstInlineSearchResults.ItemsSource = suggestions;
                    popInlineSearch.IsOpen = suggestions.Count > 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Inline search suggestion error: " + ex.Message);
                }
            }
            else
            {
                popInlineSearch.IsOpen = false;
            }
        }

        private void TxtInlineSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtInlineSearch.Text.Trim().Length >= 1)
            {
                popInlineSearch.IsOpen = true;
            }
        }

        private void TxtInlineSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            // A small delay to allow clicking suggestions before hiding
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (string.IsNullOrWhiteSpace(txtInlineSearch.Text))
                {
                    txtInlineSearch.Text = "";
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void LstInlineSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstInlineSearchResults.SelectedItem is Member member)
            {
                isSelectingSuggestion = true;
                txtInlineSearch.Text = member.MemberID;
                currentInlineSelectedMemberId = member.MemberID;
                isSelectingSuggestion = false;

                popInlineSearch.IsOpen = false;
                lstInlineSearchResults.SelectedItem = null;

                ProcessInlineCheck(member.MemberID);
            }
        }

        private void BtnInlineCheck_Click(object sender, RoutedEventArgs e)
        {
            string input = txtInlineSearch.Text.Trim();
            if (!string.IsNullOrEmpty(input))
            {
                ProcessInlineCheck(input);
            }
            else
            {
                ShowInlineAlert("Please enter a member name or ID.", false);
            }
        }

        private void BtnCollectPaymentLink_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentInlineSelectedMemberId))
            {
                brdInlineAlert.Visibility = Visibility.Collapsed;
                PaymentNavigationHelper.TryNavigateToPayment(this, currentInlineSelectedMemberId);
            }
        }

        private string? GetMemberCurrentCheckInTime(string memberId)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = @"SELECT CheckInTime FROM Attendance 
                                   WHERE MemberID = @memberId 
                                   AND CheckInDate = @today 
                                   AND (CheckOutTime IS NULL OR CheckOutTime = '')
                                   LIMIT 1";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@memberId", memberId);
                        cmd.Parameters.AddWithValue("@today", DateTime.Now.ToString("yyyy-MM-dd"));
                        object res = cmd.ExecuteScalar();
                        return res != null && res != DBNull.Value ? res.ToString() : null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error getting check in time: " + ex.Message);
                return null;
            }
        }

        private void ProcessInlineCheck(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return;

            Member? member = GetMemberByIdentifier(identifier);
            if (member == null)
            {
                ShowInlineAlert("No member found matching that search.", false);
                return;
            }

            currentInlineSelectedMemberId = member.MemberID;

            // Check status: must be Active to check in.
            // Expired or Pending members should be blocked with payment options
            if (member.Status == "Expired" || member.Status == "Pending")
            {
                string statusText = member.Status == "Expired" ? "Membership Expired." : "Membership Pending Payment.";
                ShowInlineAlert(statusText, false, showCollectLink: true);
                return;
            }

            if (member.Status != "Active")
            {
                ShowInlineAlert($"Check-in Denied. Member status is '{member.Status}'.", false);
                return;
            }

            // Member is Active! Let's check check-in vs check-out state
            string? checkInTime = GetMemberCurrentCheckInTime(member.MemberID);
            bool isCurrentlyIn = checkInTime != null;
            if (isCurrentlyIn)
            {
                // Confirmation popup before checking out
                var checkOutConfirm = MessageBox.Show(
                    $"This person is checked in at {checkInTime}. Do you want to check out?",
                    "Confirm Check-out",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (checkOutConfirm != MessageBoxResult.Yes)
                {
                    return;
                }

                // Clock them out!
                try
                {
                    using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        string sql = @"UPDATE Attendance 
                                       SET CheckOutTime = @checkOutTime 
                                       WHERE MemberID = @memberId 
                                       AND CheckInDate = @today 
                                       AND (CheckOutTime IS NULL OR CheckOutTime = '')";
                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@checkOutTime", DateTime.Now.ToString("hh:mm tt").ToUpper());
                            cmd.Parameters.AddWithValue("@memberId", member.MemberID);
                            cmd.Parameters.AddWithValue("@today", DateTime.Now.ToString("yyyy-MM-dd"));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    ShowInlineAlert($"{member.FullName} Checked Out successfully!", true);
                    txtInlineSearch.Text = "";
                    LoadDashboardData();
                }
                catch (Exception ex)
                {
                    ShowInlineAlert("Database error during check-out.", false);
                    MessageBox.Show("Error during check-out: " + ex.Message);
                }
            }
            else
            {
                // Clock them in!
                // Confirmation popup before checking in
                var checkInConfirm = MessageBox.Show(
                    $"Are you sure you want to check in {member.FullName}?",
                    "Confirm Check-in",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (checkInConfirm != MessageBoxResult.Yes)
                {
                    return;
                }

                // Check capacity before clocking in
                int currentlyInsideCount = 0;
                int maxCapacity = 100;

                try
                {
                    using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM Attendance WHERE CheckInDate = @today AND (CheckOutTime IS NULL OR CheckOutTime = '')", conn))
                        {
                            cmd.Parameters.AddWithValue("@today", DateTime.Now.ToString("yyyy-MM-dd"));
                            currentlyInsideCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                        }
                    }
                }
                catch { }

                var profile = DatabaseHelper.GetGymProfile();
                if (int.TryParse(profile.GetValueOrDefault("MaxCapacity", "100"), out int cap))
                {
                    maxCapacity = cap;
                }

                if (currentlyInsideCount >= maxCapacity)
                {
                    var capResult = MessageBox.Show(
                        $"Warning: The gym has reached maximum capacity ({maxCapacity}/{maxCapacity}).\n\nAre you sure you want to check in {member.FullName} anyway?",
                        "Max Capacity Reached",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (capResult != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                try
                {
                    using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        string sql = "INSERT INTO Attendance (MemberID, CheckInTime, CheckInDate, CheckOutTime) VALUES (@memberId, @checkInTime, @checkInDate, NULL)";
                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@memberId", member.MemberID);
                            cmd.Parameters.AddWithValue("@checkInTime", DateTime.Now.ToString("hh:mm tt").ToUpper());
                            cmd.Parameters.AddWithValue("@checkInDate", DateTime.Now.ToString("yyyy-MM-dd"));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    ShowInlineAlert($"{member.FullName} Checked In successfully!", true);
                    txtInlineSearch.Text = "";
                    LoadDashboardData();
                }
                catch (Exception ex)
                {
                    ShowInlineAlert("Database error during check-in.", false);
                    MessageBox.Show("Error during check-in: " + ex.Message);
                }
            }
        }

        private Member? GetMemberByIdentifier(string identifier)
        {
            DatabaseHelper.RefreshMemberStatuses();
            Member? member = null;
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    // Try exact match first
                    string sql = "SELECT * FROM Members WHERE LOWER(MemberID) = @identifier OR LOWER(FullName) = @identifier";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@identifier", identifier.ToLower());
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                member = ReadMemberFromReader(reader);
                            }
                        }
                    }

                    // Try partial name matches if not found
                    if (member == null)
                    {
                        string sqlLike = "SELECT * FROM Members WHERE FullName LIKE @q OR MemberID LIKE @q LIMIT 1";
                        using (var cmd = new SQLiteCommand(sqlLike, conn))
                        {
                            cmd.Parameters.AddWithValue("@q", "%" + identifier + "%");
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    member = ReadMemberFromReader(reader);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database error in GetMemberByIdentifier: " + ex.Message);
            }
            return member;
        }

        private Member ReadMemberFromReader(SQLiteDataReader reader)
        {
            string statusVal = reader["Status"]?.ToString() ?? string.Empty;
            string expDateStr = reader["ExpiryDate"]?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(expDateStr) && DateTime.TryParse(expDateStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime expDate))
            {
                if (DateTime.Today > expDate.Date)
                {
                    statusVal = "Expired";
                }
                else if (statusVal == "Expired")
                {
                    statusVal = "Active";
                }
            }

            return new Member
            {
                MemberID = reader["MemberID"]?.ToString() ?? string.Empty,
                FirstName = reader["FirstName"]?.ToString() ?? "",
                MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                LastName = reader["LastName"]?.ToString() ?? "",
                ExpiryDate = expDateStr,
                Status = statusVal
            };
        }

        private bool IsMemberAlreadyTimedIn(string memberId)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = @"SELECT COUNT(*) FROM Attendance 
                                   WHERE MemberID = @memberId 
                                   AND CheckInDate = @today 
                                   AND (CheckOutTime IS NULL OR CheckOutTime = '')";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@memberId", memberId);
                        cmd.Parameters.AddWithValue("@today", DateTime.Now.ToString("yyyy-MM-dd"));
                        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error checking duplicate check-in: " + ex.Message);
                return false;
            }
        }

        private void ShowInlineAlert(string message, bool isSuccess, bool showCollectLink = false)
        {
            txtInlineAlertMessage.Text = message;

            if (isSuccess)
            {
                brdInlineAlert.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1e3a2a"));
                txtInlineAlertIcon.Text = "✓";
                txtInlineAlertIcon.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2ecc71"));
                btnCollectPaymentLink.Visibility = Visibility.Collapsed;
            }
            else
            {
                brdInlineAlert.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3b1214"));
                txtInlineAlertIcon.Text = "✕";
                txtInlineAlertIcon.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ff4444"));
                btnCollectPaymentLink.Visibility = showCollectLink ? Visibility.Visible : Visibility.Collapsed;
            }

            brdInlineAlert.Visibility = Visibility.Visible;

            if (_inlineAlertTimer == null)
            {
                _inlineAlertTimer = new DispatcherTimer();
                _inlineAlertTimer.Interval = TimeSpan.FromSeconds(5);
                _inlineAlertTimer.Tick += (s, e) =>
                {
                    brdInlineAlert.Visibility = Visibility.Collapsed;
                    _inlineAlertTimer.Stop();
                };
            }
            else
            {
                _inlineAlertTimer.Stop();
            }
            _inlineAlertTimer.Start();
        }
    }
}
