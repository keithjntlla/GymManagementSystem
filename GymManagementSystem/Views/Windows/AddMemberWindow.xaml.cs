using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.IO;
using System.Data.SQLite;
using GymManagementSystem.Models;
using System.Windows.Controls;

namespace GymManagementSystem.Views.Windows
{
    public partial class AddMemberWindow : Window
    {
        private string selectedPhotoPath = "";
        private bool isEditMode = false;
        private string editMemberId = "";
        // Permanent backup variable to safely carry historical join dates during profile modifications
        private string historicalJoinDate = "";

        public AddMemberWindow()
        {
            InitializeComponent();
            LoadDiscountTiersDropdown();
            isEditMode = false;
            // Automatically capture today's timestamp as the clean default standard baseline
            historicalJoinDate = DateTime.Now.ToString("yyyy-MM-dd");
        }

        public AddMemberWindow(Member memberToEdit)
        {
            InitializeComponent();
            LoadDiscountTiersDropdown();
            isEditMode = true;
            editMemberId = memberToEdit.MemberID;

            lblTitle.Text = "Edit Member";
            btnRegister.Content = "Update";

            txtFirstName.Text = memberToEdit.FirstName;
            txtMiddleInitial.Text = memberToEdit.MiddleInitial;
            txtLastName.Text = memberToEdit.LastName;
            txtPhone.Text = memberToEdit.Phone;
            cmbGender.Text = memberToEdit.Gender;

            // Direct string configuration mapping assignment 
            cmbMemberType.SelectedValue = memberToEdit.MemberType;

            if (memberToEdit.Birthday.HasValue)
            {
                dpBirthday.SelectedDate = memberToEdit.Birthday.Value;
            }
            else
            {
                dpBirthday.SelectedDate = null;
            }

            // ── FIXED: RECOVERY TO Programmatic Backup Storage Variable ──
            if (DateTime.TryParse(memberToEdit.DateJoined, out DateTime joinDate))
            {
                historicalJoinDate = joinDate.ToString("yyyy-MM-dd");
            }
            else
            {
                historicalJoinDate = DateTime.Now.ToString("yyyy-MM-dd");
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

        private void LoadDiscountTiersDropdown()
        {
            var tiersList = new List<string> { "Regular" };
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT TargetType FROM Discounts", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string tierName = reader["TargetType"]?.ToString() ?? "";
                            if (!string.IsNullOrWhiteSpace(tierName) && !tierName.Equals("Regular", StringComparison.OrdinalIgnoreCase))
                            {
                                tiersList.Add(tierName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to pull lookups for matrix tiers: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            cmbMemberType.ItemsSource = tiersList;
            cmbMemberType.SelectedIndex = 0;
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
            var (isFirstValid, cleanedFirst, firstError) = InputValidator.ValidateName(txtFirstName.Text);
            if (!isFirstValid)
            {
                MessageBox.Show($"First Name Error: {firstError}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtFirstName.Focus();
                return;
            }

            var (isLastValid, cleanedLast, lastError) = InputValidator.ValidateName(txtLastName.Text);
            if (!isLastValid)
            {
                MessageBox.Show($"Last Name Error: {lastError}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtLastName.Focus();
                return;
            }

            var (isPhoneValid, cleanedPhone, phoneError) = InputValidator.ValidatePhoneNumber(txtPhone.Text);
            if (!isPhoneValid)
            {
                MessageBox.Show(phoneError, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPhone.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(dpBirthday.Text))
            {
                MessageBox.Show("Birthday is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                dpBirthday.Focus();
                return;
            }

            string typedDate = dpBirthday.Text.Trim();
            string strictFormat = "dd/MM/yyyy";

            if (!DateTime.TryParseExact(typedDate, strictFormat,
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        System.Globalization.DateTimeStyles.None,
                                        out DateTime parsedBirthday))
            {
                MessageBox.Show($"Invalid date entry. Please follow the required format precisely: {strictFormat.ToUpper()}.",
                                "Format Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                dpBirthday.Focus();
                return;
            }

            if (parsedBirthday > DateTime.Today)
            {
                MessageBox.Show("Birthday cannot be a date in the future.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                dpBirthday.Focus();
                return;
            }

            if (parsedBirthday < DateTime.Today.AddYears(-120))
            {
                MessageBox.Show("Please check the birth year. Entry appears incorrect.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                dpBirthday.Focus();
                return;
            }

            string birthdayStr = parsedBirthday.ToString("yyyy-MM-dd");

            var (isGenderValid, cleanedGender, genderError) = InputValidator.ValidateGender(cmbGender.Text);
            if (!isGenderValid)
            {
                MessageBox.Show(genderError, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                cmbGender.Focus();
                return;
            }

            if (DatabaseHelper.IsPhoneNumberDuplicate(cleanedPhone, isEditMode ? editMemberId : ""))
            {
                MessageBox.Show($"The phone number '{cleanedPhone}' is already assigned to an existing member. Please check your entries.",
                                "Duplicate Entry Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                txtPhone.Focus();
                return;
            }

            string middleInitial = txtMiddleInitial.Text.Trim().ToUpper();

            if (DatabaseHelper.IsNameCombinationDuplicate(cleanedFirst, middleInitial, cleanedLast, isEditMode ? editMemberId : ""))
            {
                string checkMessage = string.IsNullOrWhiteSpace(middleInitial)
                    ? $"A member named '{cleanedFirst} {cleanedLast}' is already registered.\n\nAre you sure you want to add a namesake entry?"
                    : $"A member named '{cleanedFirst} {middleInitial}. {cleanedLast}' is already registered.\n\nAre you sure you want to add a namesake entry?";

                var promptResult = MessageBox.Show(checkMessage, "Potential Duplicate Detected", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (promptResult != MessageBoxResult.Yes)
                {
                    txtFirstName.Focus();
                    return;
                }
            }

            string selectedType = cmbMemberType.SelectedValue?.ToString() ?? "Regular";

            string computedFullName = string.IsNullOrWhiteSpace(middleInitial)
                ? $"{cleanedFirst} {cleanedLast}"
                : $"{cleanedFirst} {middleInitial}. {cleanedLast}";

            if (isEditMode)
            {
                UpdateMember(cleanedFirst, middleInitial, cleanedLast, computedFullName, cleanedPhone, cleanedGender, birthdayStr, selectedType);
            }
            else
            {
                AddNewMember(cleanedFirst, middleInitial, cleanedLast, computedFullName, cleanedPhone, cleanedGender, birthdayStr, selectedType);
            }
        }

        private void AddNewMember(string firstName, string mi, string lastName, string fullName, string phone, string gender, string birthday, string memberType)
        {
            string memberId = GenerateMemberID();
            // FIXED: Fully localized fallback generation logic
            string dateJoined = historicalJoinDate;
            string expiryDate = "-";
            string status = "Pending";

            string savedPhotoPath = "";
            if (!string.IsNullOrEmpty(selectedPhotoPath))
            {
                string photoDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MemberPhotos");
                if (!Directory.Exists(photoDir)) Directory.CreateDirectory(photoDir);

                savedPhotoPath = Path.Combine(photoDir, memberId + Path.GetExtension(selectedPhotoPath));
                try { File.Copy(selectedPhotoPath, savedPhotoPath, true); } catch { }
            }

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = @"INSERT INTO Members 
                        (MemberID, FirstName, MiddleInitial, LastName, FullName, Phone, Gender, Birthday, MemberType, DateJoined, ExpiryDate, Status, PhotoPath) 
                        VALUES (@id, @fName, @mi, @lName, @fullName, @phone, @gender, @bday, @type, @joined, @expiry, @status, @photo)";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", memberId);
                        cmd.Parameters.AddWithValue("@fName", firstName);
                        cmd.Parameters.AddWithValue("@mi", mi);
                        cmd.Parameters.AddWithValue("@lName", lastName);
                        cmd.Parameters.AddWithValue("@fullName", fullName);
                        cmd.Parameters.AddWithValue("@phone", phone);
                        cmd.Parameters.AddWithValue("@gender", gender);
                        cmd.Parameters.AddWithValue("@bday", birthday);
                        cmd.Parameters.AddWithValue("@type", memberType);
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

        private void UpdateMember(string firstName, string mi, string lastName, string fullName, string phone, string gender, string birthday, string memberType)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = @"UPDATE Members 
                           SET FirstName = @fName,
                               MiddleInitial = @mi,
                               LastName = @lName,
                               FullName = @fullName, 
                               Phone = @phone, 
                               Gender = @gender, 
                               Birthday = @bday,
                               MemberType = @type,
                               DateJoined = @joined 
                           WHERE MemberID = @id";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@fName", firstName);
                        cmd.Parameters.AddWithValue("@mi", mi);
                        cmd.Parameters.AddWithValue("@lName", lastName);
                        cmd.Parameters.AddWithValue("@fullName", fullName);
                        cmd.Parameters.AddWithValue("@phone", phone);
                        cmd.Parameters.AddWithValue("@gender", gender);
                        cmd.Parameters.AddWithValue("@bday", birthday);
                        cmd.Parameters.AddWithValue("@type", memberType);
                        // FIXED: Re-maps the untouched background timestamp parameters safely 
                        cmd.Parameters.AddWithValue("@joined", historicalJoinDate);
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