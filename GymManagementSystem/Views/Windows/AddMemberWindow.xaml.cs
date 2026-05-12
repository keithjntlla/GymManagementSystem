using System;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.IO;
using System.Data.SQLite;
using GymManagementSystem.Models;

namespace GymManagementSystem.Views.Windows
{
    public partial class AddMemberWindow : Window
    {
        private string selectedPhotoPath = "";
        private bool isEditMode = false;
        private string editMemberId = "";

        public AddMemberWindow()
        {
            InitializeComponent();
            dpDateJoined.SelectedDate = DateTime.Now;
            isEditMode = false;
        }

        public AddMemberWindow(Member memberToEdit)
        {
            InitializeComponent();
            isEditMode = true;
            editMemberId = memberToEdit.MemberID;
            
            lblTitle.Text = "Edit Member";
            btnRegister.Content = "Update";
            
            txtFullName.Text = memberToEdit.FullName;
            txtPhone.Text = memberToEdit.Phone;
            cmbGender.Text = memberToEdit.Gender;
            
            if (DateTime.TryParse(memberToEdit.DateJoined, out DateTime joinDate))
            {
                dpDateJoined.SelectedDate = joinDate;
            }
            else
            {
                dpDateJoined.SelectedDate = DateTime.Now;
            }
            
            if (!string.IsNullOrEmpty(memberToEdit.PhotoPath) && File.Exists(memberToEdit.PhotoPath))
            {
                try
                {
                    imgPhoto.Source = new BitmapImage(new Uri(memberToEdit.PhotoPath));
                    selectedPhotoPath = memberToEdit.PhotoPath;
                }
                catch { }
            }
        }

        private void UploadPhoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog();
            op.Title = "Select a picture";
            op.Filter = "All supported graphics|*.jpg;*.jpeg;*.png|" +
              "JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
              "Portable Network Graphic (*.png)|*.png";
            if (op.ShowDialog() == true)
            {
                selectedPhotoPath = op.FileName;
                imgPhoto.Source = new BitmapImage(new Uri(selectedPhotoPath));
            }
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            // Validate Full Name
            var (isNameValid, cleanedName, nameError) = InputValidator.ValidateName(txtFullName.Text);
            if (!isNameValid)
            {
                MessageBox.Show(nameError, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtFullName.Focus();
                return;
            }

            // Validate Phone Number
            var (isPhoneValid, cleanedPhone, phoneError) = InputValidator.ValidatePhoneNumber(txtPhone.Text);
            if (!isPhoneValid)
            {
                MessageBox.Show(phoneError, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPhone.Focus();
                return;
            }

            // Validate Gender
            var (isGenderValid, cleanedGender, genderError) = InputValidator.ValidateGender(cmbGender.Text);
            if (!isGenderValid)
            {
                MessageBox.Show(genderError, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                cmbGender.Focus();
                return;
            }

            // Validate Date Joined (only for add mode)
            if (!isEditMode)
            {
                var (isDateValid, cleanedDate, dateError) = InputValidator.ValidateDateJoined(dpDateJoined.SelectedDate);
                if (!isDateValid)
                {
                    MessageBox.Show(dateError, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    dpDateJoined.Focus();
                    return;
                }
            }

            if (isEditMode)
            {
                UpdateMember(cleanedName, cleanedPhone, cleanedGender);
            }
            else
            {
                AddNewMember(cleanedName, cleanedPhone, cleanedGender);
            }
        }

        private void AddNewMember(string fullName, string phone, string gender)
        {
            string memberId = GenerateMemberID();
            string dateJoined = dpDateJoined.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd");
            string expiryDate = "-";
            string status = "Pending";

            string savedPhotoPath = "";
            if (!string.IsNullOrEmpty(selectedPhotoPath))
            {
                string photoDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MemberPhotos");
                if (!Directory.Exists(photoDir)) Directory.CreateDirectory(photoDir);
                
                savedPhotoPath = Path.Combine(photoDir, memberId + Path.GetExtension(selectedPhotoPath));
                File.Copy(selectedPhotoPath, savedPhotoPath, true);
            }

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "INSERT INTO Members (MemberID, FullName, Phone, Gender, DateJoined, ExpiryDate, Status, PhotoPath) VALUES (@id, @name, @phone, @gender, @joined, @expiry, @status, @photo)";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", memberId);
                        cmd.Parameters.AddWithValue("@name", fullName);
                        cmd.Parameters.AddWithValue("@phone", phone);
                        cmd.Parameters.AddWithValue("@gender", gender);
                        cmd.Parameters.AddWithValue("@joined", dateJoined);
                        cmd.Parameters.AddWithValue("@expiry", expiryDate);
                        cmd.Parameters.AddWithValue("@status", status);
                        cmd.Parameters.AddWithValue("@photo", savedPhotoPath);
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show($"Member {fullName} registered successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving member: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateMember(string fullName, string phone, string gender)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "UPDATE Members SET FullName = @name, Phone = @phone, Gender = @gender WHERE MemberID = @id";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", fullName);
                        cmd.Parameters.AddWithValue("@phone", phone);
                        cmd.Parameters.AddWithValue("@gender", gender);
                        cmd.Parameters.AddWithValue("@id", editMemberId);
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show($"Member {fullName} updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error updating member: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private string GenerateMemberID()
        {
            int nextId = 1;
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    // We get the numeric part of the ID, find the MAX, and add 1
                    // This works even if middle records were deleted
                    string sql = "SELECT MAX(CAST(SUBSTR(MemberID, 4) AS INTEGER)) FROM Members";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            nextId = Convert.ToInt32(result) + 1;
                        }
                    }
                }
            }
            catch { }
            return "MEM" + nextId.ToString("D4");
        }
    }
}
