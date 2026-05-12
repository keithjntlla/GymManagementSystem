using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Data.SQLite;
using System.Collections.ObjectModel;

namespace GymManagementSystem
{
    public partial class MembersView : UserControl
    {
        public ObservableCollection<Member> MembersList { get; set; } = new ObservableCollection<Member>();

        public MembersView()
        {
            InitializeComponent();
            LoadMembers();
        }

        public void LoadMembers()
        {
            MembersList.Clear();
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT * FROM Members ORDER BY MemberID DESC";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                MembersList.Add(new Member
                                {
                                    MemberID = reader["MemberID"]?.ToString() ?? "",
                                    FullName = reader["FullName"]?.ToString() ?? "",
                                    Phone = reader["Phone"]?.ToString() ?? "",
                                    Gender = reader["Gender"]?.ToString() ?? "",
                                    DateJoined = reader["DateJoined"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"]?.ToString() ?? "",
                                    Status = reader["Status"]?.ToString() ?? "",
                                    PhotoPath = reader["PhotoPath"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                dgMembers.ItemsSource = MembersList;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading members: " + ex.Message);
            }
        }

        private void PayMember_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var member = button?.CommandParameter as Member;

            if (member != null)
            {
                // Access the MainWindow and its MainFrame to change the content
                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow != null)
                {
                    // Pass the member object to the PaymentsView constructor
                    mainWindow.MainFrame.Content = new PaymentsView(member);
                }
            }
        }

        private void AddMember_Click(object sender, RoutedEventArgs e)
        {
            AddMemberWindow addWin = new AddMemberWindow();
            addWin.Owner = Window.GetWindow(this);
            if (addWin.ShowDialog() == true)
            {
                LoadMembers();
            }
        }

        private void EditMember_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string? memberId = button?.CommandParameter as string;

            if (string.IsNullOrEmpty(memberId)) return;

            Member? memberToEdit = null;
            foreach (var member in MembersList)
            {
                if (member.MemberID == memberId)
                {
                    memberToEdit = member;
                    break;
                }
            }

            if (memberToEdit != null)
            {
                AddMemberWindow editWin = new AddMemberWindow(memberToEdit);
                editWin.Owner = Window.GetWindow(this);
                if (editWin.ShowDialog() == true)
                {
                    LoadMembers();
                }
            }
        }

        private void DeleteMember_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string? memberId = button?.CommandParameter as string;

            if (string.IsNullOrEmpty(memberId)) return;

            Member? memberToCheck = null;
            foreach (var m in MembersList)
            {
                if (m.MemberID == memberId)
                {
                    memberToCheck = m;
                    break;
                }
            }

            if (memberToCheck != null && memberToCheck.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cannot delete active members. Please deactivate the member first.",
                                "Action Denied", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to delete member {memberId}?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        string sql = "DELETE FROM Members WHERE MemberID = @id";
                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", memberId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    if (memberToCheck != null && !string.IsNullOrEmpty(memberToCheck.PhotoPath) && System.IO.File.Exists(memberToCheck.PhotoPath))
                    {
                        try { System.IO.File.Delete(memberToCheck.PhotoPath); } catch { }
                    }

                    LoadMembers();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error deleting member: " + ex.Message);
                }
            }
        }
    }

    // The Member class remains here
    public class Member
    {
        public string MemberID { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string DateJoined { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string PhotoPath { get; set; } = string.Empty;
    }
}