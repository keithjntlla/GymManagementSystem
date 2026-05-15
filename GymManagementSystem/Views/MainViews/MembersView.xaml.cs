using GymManagementSystem.Models;
using GymManagementSystem.Views.MainViews;
using GymManagementSystem.Views.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace GymManagementSystem.Views.MainViews
{
    public partial class MembersView : UserControl
    {
        public ObservableCollection<Member> MembersList { get; set; } = new ObservableCollection<Member>();
        private ICollectionView? _membersView;
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
                    // We join with Payments to get the latest membership type for each member
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
                _membersView = CollectionViewSource.GetDefaultView(MembersList);
                _membersView.Filter = MemberFilterLogic;
                dgMembers.ItemsSource = MembersList;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading members: " + ex.Message);
            }
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            // Refresh the table whenever user types OR changes the dropdown
            _membersView?.Refresh();
        }

        private bool MemberFilterLogic(object obj)
        {
            if (obj is Member member)
            {
                // 1. Text Search Logic
                string searchText = txtSearch.Text.Trim().ToLower();
                bool matchesText = string.IsNullOrEmpty(searchText) ||
                                   member.FullName.ToLower().Contains(searchText) ||
                                   member.MemberID.ToLower().Contains(searchText); 

        // 2. Status Dropdown Logic
        string selectedStatus = (cbStatusFilter.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All Status";
                bool matchesStatus = selectedStatus == "All Status" || member.Status == selectedStatus; 

        return matchesText && matchesStatus;
            }
            return false;
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
                    mainWindow.btnNavPayments.IsChecked = true;
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