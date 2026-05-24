using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GymManagementSystem.Models;

namespace GymManagementSystem.Views.Windows
{
    public partial class InstructorDetailsWindow : Window
    {
        public InstructorDetailsWindow(Instructor instructor)
        {
            InitializeComponent();
            this.DataContext = instructor;
            PopulateInstructorDetails(instructor);
        }

        private void PopulateInstructorDetails(Instructor instructor)
        {
            if (instructor == null) return;

            // 1. Populate Text fields
            txtFullName.Text = instructor.FullName;
            txtInstructorID.Text = instructor.InstructorID;
            txtPhone.Text = string.IsNullOrWhiteSpace(instructor.Phone) ? "N/A" : instructor.Phone;
            txtGender.Text = string.IsNullOrWhiteSpace(instructor.Gender) ? "N/A" : instructor.Gender;
            txtBirthday.Text = instructor.Birthday.HasValue ? instructor.Birthday.Value.ToString("yyyy-MM-dd") : "N/A";
            txtDateJoined.Text = string.IsNullOrWhiteSpace(instructor.DateHired) ? "N/A" : instructor.DateHired;
            txtClientsCount.Text = $"{instructor.ClientCount} Clients";

            // 2. Color-code Status Badge
            txtStatus.Text = (instructor.Status ?? "INACTIVE").ToUpper();
            if (instructor.Status != null && instructor.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                brdStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a3d2b"));
                txtStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4caf50"));
            }
            else
            {
                brdStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B1214"));
                txtStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff3333"));
            }

            // 3. Render Initials Fallback
            string initials = "";
            if (!string.IsNullOrWhiteSpace(instructor.FirstName)) initials += instructor.FirstName[0];
            if (!string.IsNullOrWhiteSpace(instructor.LastName)) initials += instructor.LastName[0];
            if (string.IsNullOrEmpty(initials)) initials = "I";
            txtInitials.Text = initials.ToUpper();

            // 4. Load Perfect Circle Image safely
            if (!string.IsNullOrEmpty(instructor.PhotoPath) && File.Exists(instructor.PhotoPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(instructor.PhotoPath);
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

        private void ViewClients_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is Instructor instructor)
            {
                InstructorClientsWindow clientsWin = new InstructorClientsWindow(instructor);
                clientsWin.Owner = this;
                clientsWin.ShowDialog();
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
