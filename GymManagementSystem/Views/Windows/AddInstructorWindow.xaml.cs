using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Data.SQLite;
using GymManagementSystem.Models;
using System.Windows.Controls;

namespace GymManagementSystem.Views.Windows
{
    public partial class AddInstructorWindow : Window
    {
        private string selectedPhotoPath = "";
        private bool isEditMode = false;
        private string editInstructorId = "";
        private ValidationHelper _validationHelper = null!;

        public AddInstructorWindow()
        {
            InitializeComponent();
            isEditMode = false;
            InitializeValidation();
        }

        public AddInstructorWindow(Instructor instructorToEdit)
        {
            InitializeComponent();
            isEditMode = true;
            editInstructorId = instructorToEdit.InstructorID;

            lblTitle.Text = "Edit Instructor";
            btnSave.Content = "Update";

            txtFirstName.Text = instructorToEdit.FirstName;
            txtMiddleInitial.Text = instructorToEdit.MiddleInitial;
            txtLastName.Text = instructorToEdit.LastName;
            txtPhone.Text = instructorToEdit.Phone;
            txtSpecialization.Text = instructorToEdit.Specialization;

            dpBirthday.SelectedDate = instructorToEdit.Birthday;

            foreach (ComboBoxItem item in cmbGender.Items)
            {
                if (item.Content?.ToString() == instructorToEdit.Gender)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            foreach (ComboBoxItem item in cmbStatus.Items)
            {
                if (item.Content?.ToString() == instructorToEdit.Status)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(instructorToEdit.PhotoPath) && File.Exists(instructorToEdit.PhotoPath))
            {
                try
                {
                    imgPhoto.Source = new BitmapImage(new Uri(instructorToEdit.PhotoPath));
                    selectedPhotoPath = instructorToEdit.PhotoPath;
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

                if (DatabaseHelper.IsInstructorPhoneNumberDuplicate(cleaned, isEditMode ? editInstructorId : ""))
                {
                    return (false, cleaned, "Phone number is already assigned to an existing instructor.");
                }
                return (true, cleaned, "");
            });

            _validationHelper.RegisterDatePicker(dpBirthday, lblBirthdayError, InputValidator.ValidateInstructorBirthday);
            _validationHelper.RegisterComboBox(cmbGender, lblGenderError, InputValidator.ValidateGender);
            _validationHelper.RegisterTextBox(txtSpecialization, lblSpecializationError, InputValidator.ValidateSpecialization);
        }

        private void UploadPhoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog
            {
                Title = "Select a picture",
                Filter = "All supported graphics|*.jpg;*.jpeg;*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|Portable Network Graphic (*.png)|*.png"
            };
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

        private string GenerateInstructorID()
        {
            string newId = "INS0001";
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT MAX(CAST(SUBSTR(InstructorID, 4) AS INTEGER)) FROM Instructors", conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value && result.ToString() != "")
                        {
                            int maxId = Convert.ToInt32(result);
                            newId = $"INS{(maxId + 1):D4}";
                        }
                    }
                }
            }
            catch { }
            return newId;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!_validationHelper.ValidateAll()) return;

            // Birthday duplication guard
            if (dpBirthday.SelectedDate == null) return;
            string birthdayStr = dpBirthday.SelectedDate.Value.ToString("yyyy-MM-dd");

            string cleanedFirst = txtFirstName.Text.Trim();
            string cleanedLast = txtLastName.Text.Trim();

            if (DatabaseHelper.IsInstructorNameAndBirthdayDuplicate(cleanedFirst, cleanedLast, birthdayStr, isEditMode ? editInstructorId : ""))
            {
                MessageBox.Show("Cannot save instructor because an instructor with the same first name, last name, and birthday already exists. This might be the same person.",
                                "Duplicate Instructor Blocked",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                txtFirstName.Focus();
                return;
            }

            string id = isEditMode ? editInstructorId : GenerateInstructorID();
            string finalPhotoPath = "";

            if (!string.IsNullOrEmpty(selectedPhotoPath))
            {
                if (selectedPhotoPath.Contains("InstructorPhotos") && File.Exists(selectedPhotoPath))
                {
                    finalPhotoPath = selectedPhotoPath;
                }
                else
                {
                    try
                    {
                        string directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "InstructorPhotos");
                        if (!Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }
                        string fileExtension = Path.GetExtension(selectedPhotoPath);
                        string uniqueFileName = $"{id}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                        string destPath = Path.Combine(directoryPath, uniqueFileName);

                        File.Copy(selectedPhotoPath, destPath, true);
                        finalPhotoPath = destPath;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to save profile picture: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql;

                    if (isEditMode)
                    {
                        sql = @"UPDATE Instructors SET 
                                FirstName = @fn, MiddleInitial = @mi, LastName = @ln, 
                                Phone = @phone, Specialization = @spec, 
                                Status = @status, PhotoPath = @photo,
                                Birthday = @bday, Gender = @gender
                                WHERE InstructorID = @id";
                    }
                    else
                    {
                        sql = @"INSERT INTO Instructors 
                                (InstructorID, FirstName, MiddleInitial, LastName, Phone, Specialization, Status, PhotoPath, DateHired, Birthday, Gender) 
                                VALUES 
                                (@id, @fn, @mi, @ln, @phone, @spec, @status, @photo, @hire, @bday, @gender)";
                    }

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@fn", cleanedFirst);
                        cmd.Parameters.AddWithValue("@mi", txtMiddleInitial.Text.Trim());
                        cmd.Parameters.AddWithValue("@ln", cleanedLast);
                        cmd.Parameters.AddWithValue("@phone", txtPhone.Text.Trim());
                        cmd.Parameters.AddWithValue("@spec", txtSpecialization.Text.Trim());
                        cmd.Parameters.AddWithValue("@status", (cmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Active");
                        cmd.Parameters.AddWithValue("@photo", finalPhotoPath);
                        cmd.Parameters.AddWithValue("@bday", birthdayStr);
                        cmd.Parameters.AddWithValue("@gender", (cmbGender.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Male");
                        if (!isEditMode)
                        {
                            cmd.Parameters.AddWithValue("@hire", DateTime.Today.ToString("yyyy-MM-dd"));
                        }

                        cmd.ExecuteNonQuery();
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving instructor: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
