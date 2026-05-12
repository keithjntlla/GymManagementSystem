using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GymManagementSystem
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Load the dashboard by default
            MainFrame.Content = new HomeView();
        }

        private void NavHome_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Content = new HomeView();
        }

        private void NavMembers_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Content = new MembersView();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow login = new LoginWindow();
            login.Show();
            this.Close();
        }
    }
}