using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.IO;
using GymManagementSystem.Views.Settings;

namespace GymManagementSystem.Views.Settings
{
    public partial class GeneralSettingsView : UserControl
    {
        private string selectedLogoPath = "";
        private ValidationHelper _validationHelper = null!;

        public GeneralSettingsView()
        {
            InitializeComponent();
            LoadSettings();
            InitializeValidation();

            // Attach listeners to show "unsaved changes" hint
            txtGymName.TextChanged += DataChanged;
            txtAddress.TextChanged += DataChanged;
            txtContact.TextChanged += DataChanged;
            txtEmail.TextChanged += DataChanged;
            txtMaxCapacity.TextChanged += DataChanged;
        }

        private void InitializeValidation()
        {
            _validationHelper = new ValidationHelper();

            _validationHelper.RegisterTextBox(txtGymName, lblGymNameError, InputValidator.ValidateGymName);
            _validationHelper.RegisterTextBox(txtAddress, lblAddressError, InputValidator.ValidateAddress);
            _validationHelper.RegisterTextBox(txtContact, lblContactError, input => InputValidator.ValidatePhoneNumber(input, "Contact number"));
            _validationHelper.RegisterTextBox(txtEmail, lblEmailError, InputValidator.ValidateEmail);
            _validationHelper.RegisterTextBox(txtMaxCapacity, lblMaxCapacityError, input => {
                if (string.IsNullOrWhiteSpace(input)) return (false, "", "Maximum capacity is required.");
                if (!int.TryParse(input, out int cap) || cap <= 0) return (false, "", "Maximum capacity must be a positive number.");
                return (true, input, "");
            });
        }

        private void DataChanged(object sender, TextChangedEventArgs e)
        {
            if (lblUnsavedHint != null) lblUnsavedHint.Visibility = Visibility.Visible;
        }

        private void LoadSettings()
        {
            var profile = DatabaseHelper.GetGymProfile();
            // Setting .Text directly ensures current name is shown, not just a hint
            txtGymName.Text = profile.GetValueOrDefault("GymName", "");
            txtAddress.Text = profile.GetValueOrDefault("Address", "");
            txtContact.Text = profile.GetValueOrDefault("ContactNumber", "");
            txtEmail.Text = profile.GetValueOrDefault("Email", "");
            txtMaxCapacity.Text = profile.GetValueOrDefault("MaxCapacity", "100");

            selectedLogoPath = profile.GetValueOrDefault("LogoPath", "");
            if (!string.IsNullOrEmpty(selectedLogoPath) && File.Exists(selectedLogoPath))
            {
                imgLogo.Source = new BitmapImage(new Uri(selectedLogoPath));
                txtLogoPlaceholder.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Revert to your specific default logo file
                imgLogo.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/logo.png"));
                txtLogoPlaceholder.Visibility = Visibility.Visible;
            }
            lblUnsavedHint.Visibility = Visibility.Collapsed;
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Restore all settings to default? This will rename the gym to 'Gym'.",
                                         "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                DatabaseHelper.RestoreDefaultProfile();
                LoadSettings(); // This refreshes the UI textboxes immediately
                _validationHelper.ClearErrors();
            }
        }

        private void BtnUploadLogo_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg" };
            if (dlg.ShowDialog() == true)
            {
                selectedLogoPath = dlg.FileName;
                imgLogo.Source = new BitmapImage(new Uri(selectedLogoPath));
                txtLogoPlaceholder.Visibility = Visibility.Collapsed;
                lblUnsavedHint.Visibility = Visibility.Visible;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!_validationHelper.ValidateAll())
            {
                return;
            }

            try
            {
                int maxCap = int.TryParse(txtMaxCapacity.Text, out int parsed) ? parsed : 100;
                DatabaseHelper.SaveGymProfile(txtGymName.Text, txtAddress.Text, txtContact.Text, txtEmail.Text, selectedLogoPath, maxCap);
                lblUnsavedHint.Visibility = Visibility.Collapsed;
                MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving settings: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}