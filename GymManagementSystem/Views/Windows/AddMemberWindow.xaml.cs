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

        private ValidationHelper _validationHelper = null!;

        public AddMemberWindow()
        {
            InitializeComponent();
            LoadDiscountTiersDropdown();
            LoadTrainersDropdown();
            isEditMode = false;
            // Automatically capture today's timestamp as the clean default standard baseline
            historicalJoinDate = DateTime.Now.ToString("yyyy-MM-dd");
            InitializeValidation();
        }

        public AddMemberWindow(Member memberToEdit)
        {
            InitializeComponent();
            LoadDiscountTiersDropdown();
            LoadTrainersDropdown();
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

            if (!string.IsNullOrEmpty(memberToEdit.AssignedInstructorID))
            {
                cmbTrainer.SelectedValue = memberToEdit.AssignedInstructorID;
            }
            else
            {
                cmbTrainer.SelectedIndex = 0;
            }

            if (memberToEdit.Birthday.HasValue)
            {
                dpBirthday.SelectedDate = memberToEdit.Birthday.Value;
            }
            else
            {
                dpBirthday.SelectedDate = null;
            }

            // ── FIXED: RECOVERY TO Programmatic Backup Storage Variable ──
            if (DateTime.TryParse(memberToEdit.DateJoined, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime joinDate))
            {
                historicalJoinDate = joinDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                historicalJoinDate = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
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

            InitializeValidation();
        }

        private void InitializeValidation()
        {
            _validationHelper = new ValidationHelper();

            _validationHelper.RegisterTextBox(txtFirstName, lblFirstNameError, input => InputValidator.ValidateName(input, "First name"));
            _validationHelper.RegisterTextBox(txtMiddleInitial, lblMiddleInitialError, InputValidator.ValidateMiddleInitial);
            _validationHelper.RegisterTextBox(txtLastName, lblLastNameError, input => InputValidator.ValidateName(input, "Last name"));

            _validationHelper.RegisterTextBox(txtPhone, lblPhoneError, input =>
            {
                var (isValid, cleaned, error) = InputValidator.ValidatePhoneNumber(input, "Phone number");
                if (!isValid) return (false, cleaned, error);

                if (DatabaseHelper.IsPhoneNumberDuplicate(cleaned, isEditMode ? editMemberId : ""))
                {
                    return (false, cleaned, "Phone number is already assigned to an existing member.");
                }
                return (true, cleaned, "");
            });

            _validationHelper.RegisterDatePicker(dpBirthday, lblBirthdayError, input =>
            {
                var (isValid, cleaned, error) = InputValidator.ValidateBirthday(input);
                if (!isValid) return (false, cleaned, error);

                string selectedType = cmbMemberType.SelectedValue?.ToString() ?? "";
                if (selectedType.Equals("Senior", StringComparison.OrdinalIgnoreCase))
                {
                    DateTime parsedDate = DateTime.ParseExact(cleaned, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
                    int age = DateTime.Today.Year - parsedDate.Year;
                    if (parsedDate > DateTime.Today.AddYears(-age))
                    {
                        age--;
                    }

                    if (age < 60)
                    {
                        return (false, cleaned, "Member must be at least 60 years old to select the Senior member type.");
                    }
                }
                return (true, cleaned, "");
            });

            _validationHelper.RegisterComboBox(cmbGender, lblGenderError, InputValidator.ValidateGender);

            _validationHelper.RegisterComboBox(cmbMemberType, lblMemberTypeError, input =>
            {
                var (isValid, cleaned, error) = InputValidator.ValidateMemberType(input);
                if (!isValid) return (false, cleaned, error);

                if (cleaned.Equals("Senior", StringComparison.OrdinalIgnoreCase))
                {
                    if (dpBirthday.SelectedDate != null)
                    {
                        int age = DateTime.Today.Year - dpBirthday.SelectedDate.Value.Year;
                        if (dpBirthday.SelectedDate.Value > DateTime.Today.AddYears(-age))
                        {
                            age--;
                        }

                        if (age < 60)
                        {
                            return (false, cleaned, "Member must be at least 60 years old to select the Senior member type.");
                        }
                    }
                }
                return (true, cleaned, "");
            });
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

        private void LoadTrainersDropdown()
        {
            var trainersList = new List<Instructor>
            {
                new Instructor { InstructorID = "", FirstName = "[ None ]", LastName = "" }
            };

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT InstructorID, FirstName, MiddleInitial, LastName FROM Instructors WHERE Status = 'Active'", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var trainer = new Instructor
                            {
                                InstructorID = reader["InstructorID"]?.ToString() ?? "",
                                FirstName = reader["FirstName"]?.ToString() ?? "",
                                MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                                LastName = reader["LastName"]?.ToString() ?? ""
                            };
                            trainersList.Add(trainer);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to pull instructors: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            cmbTrainer.ItemsSource = trainersList;
            cmbTrainer.DisplayMemberPath = "FullName";
            cmbTrainer.SelectedValuePath = "InstructorID";
            cmbTrainer.SelectedIndex = 0;
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
                var (isValid, cleaned, error) = InputValidator.ValidatePhoto(op.FileName);
                if (isValid)
                {
                    selectedPhotoPath = op.FileName;
                    try
                    {
                        imgPhoto.Source = new BitmapImage(new Uri(selectedPhotoPath));
                        lblPhotoError.Visibility = Visibility.Collapsed;
                        lblPhotoError.Text = "";
                    }
                    catch (Exception ex)
                    {
                        lblPhotoError.Visibility = Visibility.Visible;
                        lblPhotoError.Text = "Failed to load image: " + ex.Message;
                    }
                }
                else
                {
                    lblPhotoError.Visibility = Visibility.Visible;
                    lblPhotoError.Text = error;
                }
            }
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            if (!_validationHelper.ValidateAll())
            {
                return;
            }

            string cleanedFirst = txtFirstName.Text;
            string middleInitial = txtMiddleInitial.Text;
            string cleanedLast = txtLastName.Text;
            string cleanedPhone = txtPhone.Text;
            string cleanedGender = cmbGender.Text;
            string selectedType = cmbMemberType.SelectedValue?.ToString() ?? "Regular";
            string selectedTrainerId = cmbTrainer.SelectedValue?.ToString() ?? "";

            DateTime parsedBirthday = DateTime.ParseExact(dpBirthday.Text, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            string birthdayStr = parsedBirthday.ToString("yyyy-MM-dd");

            if (DatabaseHelper.IsNameAndBirthdayDuplicate(cleanedFirst, cleanedLast, birthdayStr, isEditMode ? editMemberId : ""))
            {
                MessageBox.Show("Cannot add or update member because a member with the same first name, last name, and birthday already exists. This might be the same person.",
                                "Duplicate Member Blocked",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                txtFirstName.Focus();
                return;
            }

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

            // Student ID verification prompt
            if (selectedType.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                var promptResult = MessageBox.Show(
                    "Have they already presented a Student ID?",
                    "Student ID Verification Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (promptResult != MessageBoxResult.Yes)
                {
                    MessageBox.Show(
                        "A valid Student ID must be presented and verified to register as a Student member.",
                        "Student ID Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    cmbMemberType.Focus();
                    return;
                }
            }

            string computedFullName = string.IsNullOrWhiteSpace(middleInitial)
                ? $"{cleanedFirst} {cleanedLast}"
                : $"{cleanedFirst} {middleInitial}. {cleanedLast}";

            if (isEditMode)
            {
                UpdateMember(cleanedFirst, middleInitial, cleanedLast, computedFullName, cleanedPhone, cleanedGender, birthdayStr, selectedType, selectedTrainerId);
            }
            else
            {
                AddNewMember(cleanedFirst, middleInitial, cleanedLast, computedFullName, cleanedPhone, cleanedGender, birthdayStr, selectedType, selectedTrainerId);
            }
        }

        private void AddNewMember(string firstName, string mi, string lastName, string fullName, string phone, string gender, string birthday, string memberType, string trainerId)
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
                        (MemberID, FirstName, MiddleInitial, LastName, FullName, Phone, Gender, Birthday, MemberType, DateJoined, ExpiryDate, Status, PhotoPath, AssignedInstructorID) 
                        VALUES (@id, @fName, @mi, @lName, @fullName, @phone, @gender, @bday, @type, @joined, @expiry, @status, @photo, @trainerId)";

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
                        cmd.Parameters.AddWithValue("@trainerId", trainerId);
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

        private void UpdateMember(string firstName, string mi, string lastName, string fullName, string phone, string gender, string birthday, string memberType, string trainerId)
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
                               DateJoined = @joined,
                               AssignedInstructorID = @trainerId
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
                        cmd.Parameters.AddWithValue("@trainerId", trainerId);
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