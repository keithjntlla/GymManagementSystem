using System;
using System.Windows;

namespace GymManagementSystem
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            try 
            {
                DatabaseHelper.InitializeDatabase();
                DatabaseHelper.RefreshMemberStatuses();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing database: " + ex.Message);
            }
        }
    }
}
