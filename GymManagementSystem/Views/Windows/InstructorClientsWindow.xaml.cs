using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Windows;
using GymManagementSystem.Models;

namespace GymManagementSystem.Views.Windows
{
    public partial class InstructorClientsWindow : Window
    {
        private Instructor _instructor;
        public ObservableCollection<Member> ClientsList { get; set; } = new ObservableCollection<Member>();

        public InstructorClientsWindow(Instructor instructor)
        {
            InitializeComponent();
            _instructor = instructor;
            lblTrainerName.Text = $"{_instructor.FullName}'s Clients";
            LoadClients();
        }

        private void LoadClients()
        {
            ClientsList.Clear();
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT MemberID, FirstName, MiddleInitial, LastName, Phone, Status
                        FROM Members 
                        WHERE AssignedInstructorID = @trainerId 
                        ORDER BY FullName ASC";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@trainerId", _instructor.InstructorID);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var member = new Member
                                {
                                    MemberID = reader["MemberID"]?.ToString() ?? "",
                                    FirstName = reader["FirstName"]?.ToString() ?? "",
                                    MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                                    LastName = reader["LastName"]?.ToString() ?? "",
                                    Phone = reader["Phone"]?.ToString() ?? "",
                                    Status = reader["Status"]?.ToString() ?? "Pending"
                                };
                                ClientsList.Add(member);
                            }
                        }
                    }
                }

                if (ClientsList.Count == 0)
                {
                    lblNoClients.Visibility = Visibility.Visible;
                    lstClients.Visibility = Visibility.Collapsed;
                }
                else
                {
                    lblNoClients.Visibility = Visibility.Collapsed;
                    lstClients.Visibility = Visibility.Visible;
                    lstClients.ItemsSource = ClientsList;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading clients: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
