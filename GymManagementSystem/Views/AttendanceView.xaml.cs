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

namespace GymManagementSystem
{
    public partial class AttendanceView : UserControl
    {
        public ObservableCollection<AttendanceRecord> TodayAttendance { get; set; } = new ObservableCollection<AttendanceRecord>();
        private DispatcherTimer _alertTimer;
        private ObservableCollection<Member> _searchResults = new ObservableCollection<Member>();

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
                {
                    ShowCheckInConfirmation(selectedMember);
                }
                else
                {
                    ProcessCheckIn(txtMemberSearch.Text.Trim());
                }
                e.Handled = true;
            }
        }

        private void TxtMemberSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = txtMemberSearch.Text.Trim();
            if (query.Length >= 1 && query != (string)txtMemberSearch.Tag)
            {
                SearchMembers(query);
            }
            else
            {
                popSearch.IsOpen = false;
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
                ShowCheckInConfirmation(member);
                popSearch.IsOpen = false;
                txtMemberSearch.Clear();
                txtMemberSearch.Focus();
            }
        }

        /// <summary>
        /// Checks if a member is already timed in for today (has an active session with no Time Out).
        /// </summary>
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
                        int count = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error checking duplicate check-in: " + ex.Message);
                return false;
            }
        }

        private void ShowCheckInConfirmation(Member member)
        {
            // Check if member is Active
            if (member.Status != "Active")
            {
                UpdateStatusIndicator("Check-in Denied", "✕", Colors.Red, "#ff3333");
                DisplayMemberMiniProfile(member);
                ShowAlert($"Error: {member.Status} members cannot check in.", "#ff3333");
                return;
            }

            // Check if membership is expired
            if (DateTime.TryParse(member.ExpiryDate, out DateTime expiryDate) && expiryDate < DateTime.Now)
            {
                UpdateStatusIndicator("Membership Expired", "✕", Colors.Red, "#ff3333");
                DisplayMemberMiniProfile(member);
                ShowAlert("Error: This member's membership has expired.", "#ff3333");
                return;
            }

            // Check for duplicate check-in (already timed in today with no time out)
            if (IsMemberAlreadyTimedIn(member.MemberID))
            {
                UpdateStatusIndicator("Already Timed In", "✕", Colors.Red, "#ff3333");
                DisplayMemberMiniProfile(member);
                ShowAlert("Action Denied: This member is already timed in for today.", "#ff3333");
                return;
            }

            // Show confirmation dialog
            MessageBoxResult result = MessageBox.Show(
                $"Are you sure you want to check {member.FullName} in?",
                "Confirm Check-in",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                ProcessCheckIn(member);
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

                // Updated colors here
                UpdateStatusIndicator("Access Granted", "✓", Color.FromRgb(47, 205, 112), "#1e3a2a");
                DisplayMemberMiniProfile(member);
                LoadTodayAttendance();
                // Updated colors here
                ShowAlert("Check-in Successful!", "#1e3a2a");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error logging attendance: " + ex.Message);
            }
        }

        private void ProcessCheckIn(string memberIdentifier)
        {
            if (string.IsNullOrWhiteSpace(memberIdentifier))
            {
                ResetStatusIndicator();
                return;
            }

            Member? member = GetMemberByIdentifier(memberIdentifier);

            if (member == null)
            {
                UpdateStatusIndicator("No Member Found", "?", Colors.Yellow, "#ffcc00");
                DisplayMemberMiniProfile(null);
                ShowAlert("No Member Found", "#ffcc00");
            }
            else
            {
                ShowCheckInConfirmation(member);
            }
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

        private void UpdateStatusIndicator(string message, string icon, Color iconColor, string bgColorHex)
        {
            brdStatusIndicator.Background = (Brush?)new BrushConverter().ConvertFromString(bgColorHex) ?? Brushes.Transparent;

            txtStatusIcon.Text = icon;
            txtStatusMessage.Text = message;

            // Set the icon to the specific color (Red/Green/Yellow)
            txtStatusIcon.Foreground = new SolidColorBrush(iconColor);

            // Force the message text to be White for better readability on dark backgrounds
            txtStatusMessage.Foreground = Brushes.White;
        }

        private void ResetStatusIndicator()
        {
            // Fix CS8600 by providing a fallback Brush (Brushes.DimGray) if conversion returns null
            brdStatusIndicator.Background = (Brush?)new BrushConverter().ConvertFromString("#333") ?? Brushes.DimGray;

            txtStatusIcon.Text = "";
            txtStatusMessage.Text = "AWAITING INPUT";
            txtStatusIcon.Foreground = Brushes.White;
            txtStatusMessage.Foreground = Brushes.White;
            DisplayMemberMiniProfile(null);
        }

        private void DisplayMemberMiniProfile(Member? member)
        {
            if (member != null)
            {
                brdMemberProfile.Visibility = Visibility.Visible;
                lblMemberName.Text = member.FullName;
                lblMembershipType.Text = GetMembershipTypeFromExpiry(member.ExpiryDate);

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
                    try
                    {
                        imgProfilePicture.Source = new BitmapImage(new Uri(member.PhotoPath));
                    }
                    catch
                    {
                        imgProfilePicture.Source = null;
                    }
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

        private string GetMembershipTypeFromExpiry(string expiryDateString)
        {
            if (DateTime.TryParse(expiryDateString, out DateTime expiryDate))
            {
                TimeSpan remaining = expiryDate - DateTime.Now;
                if (remaining.TotalDays <= 1) return "Daily";
                if (remaining.TotalDays <= 7) return "Weekly";
                if (remaining.TotalDays <= 15) return "Half-Month";
                if (remaining.TotalDays <= 30) return "Monthly";
                if (remaining.TotalDays <= 365) return "Yearly";
            }
            return "Unknown";
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
                    string sql = @"SELECT A.AttendanceID, A.CheckInTime, A.CheckOutTime, M.MemberID, M.FullName, M.ExpiryDate, M.Status 
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
                                {
                                    memberStatus = "Expired";
                                }

                                TodayAttendance.Add(new AttendanceRecord
                                {
                                    AttendanceID = Convert.ToInt32(reader["AttendanceID"] ?? 0),
                                    MemberID = reader["MemberID"]?.ToString() ?? string.Empty,
                                    Name = reader["FullName"]?.ToString() ?? string.Empty,
                                    CheckInTime = DateTime.TryParse(reader["CheckInTime"]?.ToString(), out DateTime checkInTime) ? checkInTime.ToString("HH:mm tt") : string.Empty,
                                    CheckOutTime = reader["CheckOutTime"]?.ToString() ?? string.Empty,
                                    MembershipType = GetMembershipTypeFromExpiry(reader["ExpiryDate"]?.ToString() ?? string.Empty),
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
            // Added (Brush?) and ?? Brushes.Transparent to handle potential nulls
            brdAlertOverlay.Background = (Brush?)new BrushConverter().ConvertFromString(bgColorHex) ?? Brushes.Transparent;

            txtAlertMessage.Text = message;
            txtAlertIcon.Text = message.Contains("Successful") ? "✓" : (message.Contains("Expired") || message.Contains("Error") || message.Contains("Denied") ? "✕" : "?");
            brdAlertOverlay.Visibility = Visibility.Visible;
            _alertTimer.Start();
        }

        private void TimeOut_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is int attendanceId)
            {
                // 1. Find the record in the current list to check its status
                AttendanceRecord? recordToCheck = null;
                foreach (var record in TodayAttendance)
                {
                    if (record.AttendanceID == attendanceId)
                    {
                        recordToCheck = record;
                        break;
                    }
                }

                // 2. Logic Check: If CheckOutTime is not empty, they already timed out
                if (recordToCheck != null && !string.IsNullOrEmpty(recordToCheck.CheckOutTime))
                {
                    MessageBox.Show("This member has already timed out for this session.",
                                    "Action Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. Proceed with confirmation if not yet timed out
                MessageBoxResult result = MessageBox.Show(
                    "Are you sure you want to check this member out?",
                    "Confirm Check-out",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    ProcessCheckOut(attendanceId);
                }
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
                // Updated colors here
                ShowAlert("Check-out Successful!", "#1e3a2a");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error logging check-out: " + ex.Message);
            }
        }
    }
}
