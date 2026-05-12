using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GymManagementSystem
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void btnSignIn_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text;
            string password = txtPassword.Password; 

            if (username == "admin" && password == "admin123")
            {
                MessageBox.Show("Welcome, Administrator!", "Login Success");
                OpenDashboard();
            }
            else if (username == "staff" && password == "staff123")
            {
                MessageBox.Show("Welcome, Staff Member!", "Login Success");
                OpenDashboard();
            }
            else
            {
                MessageBox.Show("Invalid username or password. Please try again.", "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenDashboard()
        {
            MainWindow main = new MainWindow();
            main.Show();
            this.Close(); // Close the login window
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}