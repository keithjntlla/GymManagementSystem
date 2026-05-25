using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GymManagementSystem.Models;

namespace GymManagementSystem.Views.Windows
{
    public partial class MemberDetailsWindow : Window
    {
        public MemberDetailsWindow(Member member)
        {
            InitializeComponent();
            this.DataContext = member;
            PopulateMemberDetails(member);
        }

        private void PopulateMemberDetails(Member member)
        {
            if (member == null) return;

            // 1. Populate Text fields
            txtFullName.Text = member.FullName;
            txtMemberID.Text = member.MemberID;
            txtMemberType.Text = member.MemberType;
            txtPhone.Text = string.IsNullOrWhiteSpace(member.Phone) ? "N/A" : member.Phone;
            txtGender.Text = string.IsNullOrWhiteSpace(member.Gender) ? "N/A" : member.Gender;
            txtBirthday.Text = member.Birthday.HasValue ? member.Birthday.Value.ToString("yyyy-MM-dd") : "N/A";
            txtDateJoined.Text = string.IsNullOrWhiteSpace(member.DateJoined) ? "N/A" : member.DateJoined;
            txtExpiryDate.Text = string.IsNullOrWhiteSpace(member.ExpiryDate) ? "-" : member.ExpiryDate;

            // 1.5. Calculate and display remaining days of current plan
            if (string.IsNullOrWhiteSpace(member.ExpiryDate) || member.ExpiryDate == "-")
            {
                txtDaysRemaining.Text = "No active plan";
                txtDaysRemaining.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"));
            }
            else
            {
                int days = ExpirationUiHelper.CalcDaysRemaining(member.ExpiryDate);
                if (days < 0)
                {
                    txtDaysRemaining.Text = $"Expired ({Math.Abs(days)} days ago)";
                    txtDaysRemaining.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff3333"));
                }
                else if (days == 0)
                {
                    txtDaysRemaining.Text = "Expires Today";
                    txtDaysRemaining.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff3333"));
                }
                else if (days == 1)
                {
                    txtDaysRemaining.Text = "1 day remaining";
                    txtDaysRemaining.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffcc00"));
                }
                else if (days <= 3)
                {
                    txtDaysRemaining.Text = $"{days} days remaining";
                    txtDaysRemaining.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffcc00"));
                }
                else
                {
                    txtDaysRemaining.Text = $"{days} days remaining";
                    txtDaysRemaining.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4caf50"));
                }
            }

            // 2. Color-code Status Badge
            txtStatus.Text = (member.Status ?? "PENDING").ToUpper();
            if (member.Status != null && member.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                brdStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a3d2b"));
                txtStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4caf50"));
            }
            else if (member.Status != null && member.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                brdStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3a2010"));
                txtStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffcc00"));
            }
            else
            {
                brdStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B1214"));
                txtStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff3333"));
            }

            // 3. Render Initials Fallback
            string initials = "";
            if (!string.IsNullOrWhiteSpace(member.FirstName)) initials += member.FirstName[0];
            if (!string.IsNullOrWhiteSpace(member.LastName)) initials += member.LastName[0];
            if (string.IsNullOrEmpty(initials)) initials = "M";
            txtInitials.Text = initials.ToUpper();

            // 4. Load Perfect Circle Image safely
            if (!string.IsNullOrEmpty(member.PhotoPath) && File.Exists(member.PhotoPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(member.PhotoPath);
                    bitmap.EndInit();

                    imgBrush.ImageSource = bitmap;
                    ellPhoto.Visibility = Visibility.Visible;
                }
                catch
                {
                    ellPhoto.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ellPhoto.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
