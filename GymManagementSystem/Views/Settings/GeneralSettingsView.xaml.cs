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

        public GeneralSettingsView()
        {
            InitializeComponent();
            LoadSettings();

            // Attach listeners to show "unsaved changes" hint
            txtGymName.TextChanged += DataChanged;
            txtAddress.TextChanged += DataChanged;
            txtContact.TextChanged += DataChanged;
            txtEmail.TextChanged += DataChanged;
        }

        private void DataChanged(object sender, TextChangedEventArgs e)
        {
            if (lblUnsavedHint != null) lblUnsavedHint.Visibility = Visibility.Visible;
        }

        private void LoadSettings()
        {
            var profile = DatabaseHelper.GetGymProfile();
            // Setting .Text directly ensures current name is shown, not just a hint[cite: 23]
            txtGymName.Text = profile.GetValueOrDefault("GymName", "Gym");
            txtAddress.Text = profile.GetValueOrDefault("Address", "");
            txtContact.Text = profile.GetValueOrDefault("ContactNumber", "");
            txtEmail.Text = profile.GetValueOrDefault("Email", "");

            selectedLogoPath = profile.GetValueOrDefault("LogoPath", "");
            if (!string.IsNullOrEmpty(selectedLogoPath) && File.Exists(selectedLogoPath))
            {
                imgLogo.Source = new BitmapImage(new Uri(selectedLogoPath));
                txtLogoPlaceholder.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Revert to your specific default logo file[cite: 18]
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
                LoadSettings(); // This refreshes the UI textboxes immediately[cite: 23]
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
            try
            {
                DatabaseHelper.SaveGymProfile(txtGymName.Text, txtAddress.Text, txtContact.Text, txtEmail.Text, selectedLogoPath);
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