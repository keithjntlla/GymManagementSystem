using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Data.SQLite;
using System.Collections.ObjectModel;
using GymManagementSystem.Models;
using GymManagementSystem.Views.MainViews;
using GymManagementSystem.Views.Windows;

namespace GymManagementSystem.Views.MainViews
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
                    // We join with Payments to get the latest membership type for each member[cite: 1, 15]
                    string sql = @"
                        SELECT M.*, 
                        COALESCE((SELECT P.MembershipType FROM Payments P WHERE P.MemberID = M.MemberID ORDER BY P.PaymentID DESC LIMIT 1), '-') as PlanName
                        FROM Members M 
                        ORDER BY M.MemberID DESC";

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
                                    MembershipPlan = reader["PlanName"]?.ToString() ?? "-",
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

            if (memberToCheck == null) return;

            // 1. Handle Active Members
            if (memberToCheck.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cannot delete active members",
                                "Action Denied", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            // 2. Handle Expired Members (Immediate Block Message)
            if (memberToCheck.Status.Equals("Expired", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show($"Cannot delete member {memberId} because they have existing history in the system.\n\n" +
                                "To preserve financial and gym records, these members must remain in the database.",
                                "Records Protected", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            // 3. Handle Pending Members (Only these get the "Are you sure" confirmation)
            if (memberToCheck.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                if (MessageBox.Show($"Are you sure you want to delete member {memberId}?\n\nThis will permanently remove their profile.",
                                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
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

                        // Cleanup photo if database delete was successful
                        if (!string.IsNullOrEmpty(memberToCheck.PhotoPath) && System.IO.File.Exists(memberToCheck.PhotoPath))
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
    }
}