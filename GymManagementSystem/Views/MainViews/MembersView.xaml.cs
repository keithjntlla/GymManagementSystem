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

                    // ── FIXED SQL: SEPARATES INDIVIDUAL PLANS BY COMMA, AND INTERNAL DATA BY PIPE ──
                    string sql = @"
                SELECT M.*, 
                COALESCE(
                    (
                        SELECT GROUP_CONCAT(CleanPlan, ',')
                        FROM (
                            SELECT DISTINCT 
                                CASE 
                                    WHEN INSTR(P.MembershipType, ' (') > 0 
                                    THEN SUBSTR(P.MembershipType, 1, INSTR(P.MembershipType, ' (') - 1)
                                    WHEN INSTR(P.MembershipType, '[Advance Renewal] ') > 0
                                    THEN SUBSTR(P.MembershipType, 19)
                                    WHEN INSTR(P.MembershipType, '[Advanced] ') > 0
                                    THEN SUBSTR(P.MembershipType, 12)
                                    WHEN INSTR(P.MembershipType, '[Queued] ') > 0
                                    THEN SUBSTR(P.MembershipType, 10)
                                    ELSE P.MembershipType 
                                END || '|' ||
                                CASE 
                                    WHEN INSTR(P.MembershipType, ' (') > 0 
                                    THEN 'x' || SUBSTR(P.MembershipType, INSTR(P.MembershipType, ' (') + 2, INSTR(P.MembershipType, ')') - INSTR(P.MembershipType, ' (') - 2)
                                    ELSE '' 
                                END as CleanPlan
                            FROM Payments P
                            WHERE P.MemberID = M.MemberID
                              AND Date(P.NewExpiryDate) >= Date('now')
                            ORDER BY P.PaymentID ASC
                        )
                    ), '-'
                ) as ActivePlans
                FROM Members M 
                ORDER BY M.MemberID DESC";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
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
                                    Gender = reader["Gender"]?.ToString() ?? "",

                                    // String formats output like: "Daily|x2,Weekly|"
                                    MembershipPlan = reader["ActivePlans"]?.ToString() ?? "-",

                                    Status = reader["Status"]?.ToString() ?? "",
                                    PhotoPath = reader["PhotoPath"]?.ToString() ?? ""
                                };

                                if (reader["DateJoined"] != DBNull.Value && DateTime.TryParse(reader["DateJoined"].ToString(), out DateTime joinDate))
                                {
                                    member.DateJoined = joinDate.ToString("yyyy-MM-dd");
                                }
                                else
                                {
                                    member.DateJoined = DateTime.Now.ToString("yyyy-MM-dd");
                                }

                                if (reader["ExpiryDate"] != DBNull.Value && DateTime.TryParse(reader["ExpiryDate"].ToString(), out DateTime expiryDate))
                                {
                                    member.ExpiryDate = expiryDate.ToString("yyyy-MM-dd");
                                }
                                else
                                {
                                    member.ExpiryDate = "-";
                                }

                                if (reader["Birthday"] != DBNull.Value && DateTime.TryParse(reader["Birthday"].ToString(), out DateTime bDay))
                                    member.Birthday = bDay;

                                member.MemberType = reader["MemberType"] != DBNull.Value
                                    ? reader["MemberType"].ToString() ?? "Regular"
                                    : "Regular";

                                MembersList.Add(member);
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
            _membersView?.Refresh();
        }

        private bool MemberFilterLogic(object obj)
        {
            if (obj is Member member)
            {
                string searchText = txtSearch.Text.Trim().ToLower();
                bool matchesText = string.IsNullOrEmpty(searchText) ||
                                   member.FullName.ToLower().Contains(searchText) ||
                                   member.MemberID.ToLower().Contains(searchText);

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
                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow != null)
                {
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
                var safeMemberCopy = new Member
                {
                    MemberID = memberToEdit.MemberID,
                    FirstName = memberToEdit.FirstName,
                    MiddleInitial = memberToEdit.MiddleInitial,
                    LastName = memberToEdit.LastName,
                    Phone = memberToEdit.Phone,
                    Gender = memberToEdit.Gender,
                    Birthday = memberToEdit.Birthday,
                    MemberType = memberToEdit.MemberType,
                    DateJoined = memberToEdit.DateJoined,
                    ExpiryDate = memberToEdit.ExpiryDate,
                    Status = memberToEdit.Status,
                    PhotoPath = memberToEdit.PhotoPath,
                    MembershipPlan = memberToEdit.MembershipPlan.Contains(",")
                        ? memberToEdit.MembershipPlan.Split(',')[0]
                        : memberToEdit.MembershipPlan
                };

                // Strip the pipe out from the single base plan before editing
                if (safeMemberCopy.MembershipPlan.Contains("|"))
                {
                    safeMemberCopy.MembershipPlan = safeMemberCopy.MembershipPlan.Split('|')[0];
                }

                AddMemberWindow editWin = new AddMemberWindow(safeMemberCopy);
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

            if (memberToCheck.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cannot delete active members",
                                "Action Denied", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            if (memberToCheck.Status.Equals("Expired", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show($"Cannot delete member {memberId} because they have existing history in the system.\n\n" +
                                "To preserve financial and gym records, these members must remain in the database.",
                                "Records Protected", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

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