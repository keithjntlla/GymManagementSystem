using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GymManagementSystem.Models;
using GymManagementSystem.Views.MainViews;

namespace GymManagementSystem.Views.MainViews
{
    public partial class AttendanceView : UserControl
    {
        public ObservableCollection<AttendanceRecord> TodayAttendance { get; set; } = new ObservableCollection<AttendanceRecord>();
        private DispatcherTimer _alertTimer;
        private DispatcherTimer _clockTimer;
        private ObservableCollection<Member> _searchResults = new ObservableCollection<Member>();
        private Member? _selectedMember = null;

        public AttendanceView()
        {
            InitializeComponent();
            this.DataContext = this;
            LoadTodayAttendance();
            txtMemberSearch.Focus();

            _alertTimer = new DispatcherTimer();
            _alertTimer.Interval = TimeSpan.FromSeconds(3);
            _alertTimer.Tick += (sender, e) =>
            {
                brdAlertOverlay.Visibility = Visibility.Collapsed;
                _alertTimer.Stop();
            };

            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (sender, e) => UpdateLiveTime();
            _clockTimer.Start();
            UpdateLiveTime(); // Initial call to prevent delay
        }
        private void UpdateLiveTime()
        {
            lblLiveDate.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy");
            lblLiveTime.Text = DateTime.Now.ToString("hh:mm tt").ToUpper();
        }

        private void TxtMemberSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtMemberSearch.Text == (string)txtMemberSearch.Tag)
            {
                txtMemberSearch.Text = "";
                txtMemberSearch.Foreground = Brushes.White;
            }
        }

        private void TxtMemberSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMemberSearch.Text))
            {
                txtMemberSearch.Text = (string)txtMemberSearch.Tag;
                txtMemberSearch.Foreground = Brushes.Gray;
            }
        }

        private void TxtMemberSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (lstSearchResults.SelectedItem is Member selectedMember)
                    PreviewMember(selectedMember);
                else
                    PreviewMemberByIdentifier(txtMemberSearch.Text.Trim());
                e.Handled = true;
            }
        }

        private void TxtMemberSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = txtMemberSearch.Text.Trim();
            if (query.Length >= 1 && query != (string)txtMemberSearch.Tag)
                SearchMembers(query);
            else
            {
                popSearch.IsOpen = false;
                ClearPreview();
            }
        }

        private void SearchMembers(string query)
        {
            _searchResults.Clear();
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT * FROM Members WHERE FullName LIKE @query OR MemberID LIKE @query LIMIT 10";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@query", "%" + query + "%");
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                _searchResults.Add(new Member
                                {
                                    MemberID = reader["MemberID"]?.ToString() ?? string.Empty,
                                    FullName = reader["FullName"]?.ToString() ?? string.Empty,
                                    Phone = reader["Phone"]?.ToString() ?? string.Empty,
                                    Gender = reader["Gender"]?.ToString() ?? string.Empty,
                                    ExpiryDate = reader["ExpiryDate"]?.ToString() ?? string.Empty,
                                    Status = reader["Status"]?.ToString() ?? string.Empty,
                                    PhotoPath = reader["PhotoPath"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }
                lstSearchResults.ItemsSource = _searchResults;
                popSearch.IsOpen = _searchResults.Count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Search error: " + ex.Message);
            }
        }

        private void LstSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSearchResults.SelectedItem is Member member)
            {
                // 1. Hide the "No Selection" placeholder
                panelNoMemberAttendance.Visibility = Visibility.Collapsed;

                // 2. Show the Status Indicator and Profile
                brdStatusIndicator.Visibility = Visibility.Visible; // <--- ADD THIS LINE
                brdMemberProfile.Visibility = Visibility.Visible;
                btnCheckIn.Visibility = Visibility.Visible;

                popSearch.IsOpen = false;
                txtMemberSearch.Text = member.FullName;
                txtMemberSearch.Foreground = Brushes.White;

                // 3. This method updates the text/colors, but we must ensure the border is visible first
                PreviewMember(member);
            }
        }

        private void PreviewMember(Member member)
        {
            _selectedMember = member;
            DisplayMemberMiniProfile(member);

            if (member.Status != "Active")
            {
                UpdateStatusIndicator("Check-in Denied", "✕", Colors.White, "#ff3333");
                btnCheckIn.Visibility = Visibility.Collapsed;
            }
            // 3. Check for expired membership
            else if (DateTime.TryParse(member.ExpiryDate, out DateTime expiry) && expiry.Date < DateTime.Now.Date)
            {
                UpdateStatusIndicator("Membership Expired", "✕", Colors.White, "#ff3333");
                btnCheckIn.Visibility = Visibility.Collapsed;
            }
            // 4. Check if already timed in
            else if (IsMemberAlreadyTimedIn(member.MemberID))
            {
                UpdateStatusIndicator("Already Timed In", "✕", Colors.White, "#ff3333");
                btnCheckIn.Visibility = Visibility.Collapsed;
            }
            // 5. Allow check-in if all conditions pass
            else
            {
                UpdateStatusIndicator("Ready to Check In", "✓", Color.FromRgb(47, 205, 112), "#1e3a2a");
                btnCheckIn.Visibility = Visibility.Visible;
            }
        }

        private void PreviewMemberByIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return;
            Member? member = GetMemberByIdentifier(identifier);
            if (member == null)
            {
                UpdateStatusIndicator("No Member Found", "?", Colors.Yellow, "#ffcc00");
                DisplayMemberMiniProfile(null);
                btnCheckIn.Visibility = Visibility.Collapsed;
                _selectedMember = null;
            }
            else
            {
                PreviewMember(member);
            }
        }

        private void btnCheckIn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMember == null) return;

            // Show confirmation dialog before processing
            MessageBoxResult result = MessageBox.Show(
                $"Are you sure you want to check in {_selectedMember.FullName}?",
                "Confirm Check-in",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ProcessCheckIn(_selectedMember);
            }
        }

        private void ProcessCheckIn(Member member)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "INSERT INTO Attendance (MemberID, CheckInTime, CheckInDate, CheckOutTime) VALUES (@memberId, @checkInTime, @checkInDate, NULL)";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@memberId", member.MemberID);
                        cmd.Parameters.AddWithValue("@checkInTime", DateTime.Now.ToString("HH:mm tt"));
                        cmd.Parameters.AddWithValue("@checkInDate", DateTime.Now.ToString("yyyy-MM-dd"));
                        cmd.ExecuteNonQuery();
                    }
                }

                // Show the alert at the top
                ShowAlert("Check-in Successful!", "#1e3a2a");

                // Refresh the history grid
                LoadTodayAttendance();

                // RESET UI TO DEFAULT SELECTION STATE
                _selectedMember = null;
                txtMemberSearch.Clear();

                panelNoMemberAttendance.Visibility = Visibility.Visible; // Show "Select a member first"
                brdStatusIndicator.Visibility = Visibility.Collapsed;   // Hide result box
                brdMemberProfile.Visibility = Visibility.Collapsed;      // Hide profile
                btnCheckIn.Visibility = Visibility.Collapsed;            // Hide button

                txtMemberSearch.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error logging attendance: " + ex.Message);
            }
        }

        private void ClearPreview()
        {
            _selectedMember = null;
            btnCheckIn.Visibility = Visibility.Collapsed;
            DisplayMemberMiniProfile(null);
            ResetStatusIndicator();
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
                MessageBox.Show("Error checking duplicate check-in: " + ex.Message);
                return false;
            }
        }

        private string GetMembershipTypeFromPayments(string memberId)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = @"SELECT MembershipType FROM Payments 
                                   WHERE MemberID = @memberId 
                                   ORDER BY PaymentID DESC LIMIT 1";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@memberId", memberId);
                        object? result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            return result.ToString() ?? "Unknown";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error getting membership type: " + ex.Message);
            }
            return "Unknown";
        }

        private Member? GetMemberByIdentifier(string identifier)
        {
            Member? member = null;
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT * FROM Members WHERE MemberID = @identifier OR FullName LIKE @identifier";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@identifier", identifier);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                member = new Member
                                {
                                    MemberID = reader["MemberID"]?.ToString() ?? string.Empty,
                                    FullName = reader["FullName"]?.ToString() ?? string.Empty,
                                    Phone = reader["Phone"]?.ToString() ?? string.Empty,
                                    Gender = reader["Gender"]?.ToString() ?? string.Empty,
                                    DateJoined = reader["DateJoined"]?.ToString() ?? string.Empty,
                                    ExpiryDate = reader["ExpiryDate"]?.ToString() ?? string.Empty,
                                    Status = reader["Status"]?.ToString() ?? string.Empty,
                                    PhotoPath = reader["PhotoPath"]?.ToString() ?? string.Empty
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database error: " + ex.Message);
            }
            return member;
        }

        private void DisplayMemberMiniProfile(Member? member)
        {
            if (member != null)
            {
                brdMemberProfile.Visibility = Visibility.Visible;
                lblMemberName.Text = member.FullName;
                lblMembershipType.Text = GetMembershipTypeFromPayments(member.MemberID);

                if (DateTime.TryParse(member.ExpiryDate, out DateTime expiryDate))
                {
                    int daysRemaining = (int)(expiryDate - DateTime.Now).TotalDays;
                    if (daysRemaining < 0)
                    {
                        lblDaysRemaining.Text = "Expired";
                        lblDaysRemaining.Foreground = Brushes.Red;
                    }
                    else
                    {
                        lblDaysRemaining.Text = $"{daysRemaining} days";
                        lblDaysRemaining.Foreground = Brushes.LightGreen;
                    }
                }
                else
                {
                    lblDaysRemaining.Text = "N/A";
                    lblDaysRemaining.Foreground = Brushes.Gray;
                }

                if (!string.IsNullOrEmpty(member.PhotoPath) && File.Exists(member.PhotoPath))
                {
                    try { imgProfilePicture.Source = new BitmapImage(new Uri(member.PhotoPath)); }
                    catch { imgProfilePicture.Source = null; }
                }
                else
                {
                    imgProfilePicture.Source = null;
                }
            }
            else
            {
                brdMemberProfile.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateStatusIndicator(string message, string icon, Color iconColor, string bgColorHex)
        {
            brdStatusIndicator.Background = (Brush?)new BrushConverter().ConvertFromString(bgColorHex) ?? Brushes.Transparent;
            txtStatusIcon.Text = icon;
            txtStatusMessage.Text = message;
            txtStatusIcon.Foreground = new SolidColorBrush(iconColor);
            txtStatusMessage.Foreground = Brushes.White;
        }

        private void ResetStatusIndicator()
        {
            // Hide the status box (the one that says Awaiting Input/Access Granted)
            brdStatusIndicator.Visibility = Visibility.Collapsed;

            // Show the default placeholder screen
            panelNoMemberAttendance.Visibility = Visibility.Visible;

            // Clear the profile details
            DisplayMemberMiniProfile(null);
        }

        private void LoadTodayAttendance()
        {
            TodayAttendance.Clear();
            int totalMembers = 0;
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT  A.AttendanceID, A.CheckInTime, A.CheckOutTime,
                                M.MemberID, M.FullName, M.ExpiryDate, M.Status,
                                COALESCE(
                                    (SELECT P.MembershipType FROM Payments P
                                     WHERE P.MemberID = M.MemberID
                                     ORDER BY P.PaymentID DESC LIMIT 1),
                                    'Unknown') AS MembershipType
                        FROM Attendance A
                        JOIN Members M ON A.MemberID = M.MemberID
                        WHERE A.CheckInDate = @today
                        ORDER BY A.CheckInTime DESC";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@today", DateTime.Now.ToString("yyyy-MM-dd"));
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string memberStatus = reader["Status"]?.ToString() ?? string.Empty;
                                string expiryDateStr = reader["ExpiryDate"]?.ToString() ?? string.Empty;

                                if (DateTime.TryParse(expiryDateStr, out DateTime expiryDate) && expiryDate < DateTime.Now)
                                    memberStatus = "Expired";

                                TodayAttendance.Add(new AttendanceRecord
                                {
                                    AttendanceID = Convert.ToInt32(reader["AttendanceID"] ?? 0),
                                    MemberID = reader["MemberID"]?.ToString() ?? string.Empty,
                                    Name = reader["FullName"]?.ToString() ?? string.Empty,
                                    CheckInTime = DateTime.TryParse(reader["CheckInTime"]?.ToString(), out DateTime checkInTime)
                                        ? checkInTime.ToString("HH:mm tt") : string.Empty,
                                    CheckOutTime = reader["CheckOutTime"]?.ToString() ?? string.Empty,
                                    MembershipType = reader["MembershipType"]?.ToString() ?? "Unknown",
                                    Status = memberStatus
                                });
                                totalMembers++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading today's attendance: " + ex.Message);
            }
            lblTotalMembers.Text = $"Total: {totalMembers} members";
        }

        private void ShowAlert(string message, string bgColorHex)
        {
            brdAlertOverlay.Background = (Brush?)new BrushConverter().ConvertFromString(bgColorHex) ?? Brushes.Transparent;
            txtAlertMessage.Text = message;
            txtAlertIcon.Text = message.Contains("Successful") ? "✓"
                              : (message.Contains("Expired") || message.Contains("Error") || message.Contains("Denied") ? "✕" : "?");
            brdAlertOverlay.Visibility = Visibility.Visible;
            _alertTimer.Start();
        }

        private void TimeOut_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is int attendanceId)
            {
                AttendanceRecord? recordToCheck = null;
                foreach (var record in TodayAttendance)
                {
                    if (record.AttendanceID == attendanceId)
                    {
                        recordToCheck = record;
                        break;
                    }
                }

                if (recordToCheck != null && !string.IsNullOrEmpty(recordToCheck.CheckOutTime))
                {
                    MessageBox.Show("This member has already timed out for this session.",
                                    "Action Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MessageBoxResult result = MessageBox.Show(
                    "Are you sure you want to check this member out?",
                    "Confirm Check-out", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                    ProcessCheckOut(attendanceId);
            }
        }

        private void ProcessCheckOut(int attendanceId)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "UPDATE Attendance SET CheckOutTime = @checkOutTime WHERE AttendanceID = @attendanceId";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@checkOutTime", DateTime.Now.ToString("HH:mm tt"));
                        cmd.Parameters.AddWithValue("@attendanceId", attendanceId);
                        cmd.ExecuteNonQuery();
                    }
                }
                LoadTodayAttendance();
                ShowAlert("Check-out Successful!", "#1e3a2a");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error logging check-out: " + ex.Message);
            }
        }
    }
}