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
using System.Windows.Media;
using System.Windows.Input;

namespace GymManagementSystem.Views.MainViews
{
    public partial class MembersView : UserControl
    {
        public ObservableCollection<Member> MembersList { get; set; } = new ObservableCollection<Member>();
        private ICollectionView? _membersView;

        private List<CheckBox> _dynamicTypeCheckBoxes = new List<CheckBox>();
        private List<CheckBox> _dynamicPlanCheckBoxes = new List<CheckBox>();

        public MembersView()
        {
            InitializeComponent();
            LoadDynamicMemberTypes();
            LoadDynamicPlans();
            LoadMembers();
        }

        private void LoadDynamicMemberTypes()
        {
            // Save currently checked types to restore them
            var checkedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var chk in _dynamicTypeCheckBoxes)
            {
                if (chk.IsChecked == true)
                {
                    string? name = chk.Content?.ToString();
                    if (!string.IsNullOrEmpty(name)) checkedTypes.Add(name);
                }
            }

            spDynamicTypes.Children.Clear();
            _dynamicTypeCheckBoxes.Clear();

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT DISTINCT TargetType FROM Discounts", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string tierName = reader["TargetType"]?.ToString() ?? "";
                            if (!string.IsNullOrWhiteSpace(tierName) && 
                                !tierName.Equals("Regular", StringComparison.OrdinalIgnoreCase) && 
                                !tierName.Equals("Student", StringComparison.OrdinalIgnoreCase))
                            {
                                var chk = new CheckBox
                                {
                                    Content = tierName,
                                    Style = (Style)FindResource("PopupCheckBox")
                                };
                                chk.Checked += FilterCheckbox_Changed;
                                chk.Unchecked += FilterCheckbox_Changed;
                                
                                if (checkedTypes.Contains(tierName))
                                {
                                    chk.IsChecked = true;
                                }

                                spDynamicTypes.Children.Add(chk);
                                _dynamicTypeCheckBoxes.Add(chk);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to pull lookups for member type filter: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDynamicPlans()
        {
            // Save currently checked plans to restore them
            var checkedPlans = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var chk in _dynamicPlanCheckBoxes)
            {
                if (chk.IsChecked == true)
                {
                    string? name = chk.Content?.ToString();
                    if (!string.IsNullOrEmpty(name)) checkedPlans.Add(name);
                }
            }

            spDynamicPlans.Children.Clear();
            _dynamicPlanCheckBoxes.Clear();

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT DISTINCT PlanName FROM Rates WHERE IsArchived = 0 ORDER BY DurationDays ASC", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string planName = reader["PlanName"]?.ToString() ?? "";
                            if (!string.IsNullOrWhiteSpace(planName))
                            {
                                var chk = new CheckBox
                                {
                                    Content = planName,
                                    Style = (Style)FindResource("PopupCheckBox")
                                };
                                chk.Checked += FilterCheckbox_Changed;
                                chk.Unchecked += FilterCheckbox_Changed;

                                if (checkedPlans.Contains(planName))
                                {
                                    chk.IsChecked = true;
                                }

                                spDynamicPlans.Children.Add(chk);
                                _dynamicPlanCheckBoxes.Add(chk);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to pull lookups for plan filter: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SetStatusFilter(string status)
        {
            chkStatusActive.IsChecked = false;
            chkStatusPending.IsChecked = false;
            chkStatusExpired.IsChecked = false;

            if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                chkStatusActive.IsChecked = true;
            }
            else if (status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                chkStatusPending.IsChecked = true;
            }
            else if (status.Equals("Expired", StringComparison.OrdinalIgnoreCase))
            {
                chkStatusExpired.IsChecked = true;
            }

            UpdateFilterChips();
            _membersView?.Refresh();
        }

        public void LoadMembers()
        {
            DatabaseHelper.RefreshMemberStatuses();
            MembersList.Clear();
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    string sql = @"
                                SELECT M.*, 
                                COALESCE(
                                    (
                                        SELECT 
                                            (SELECT P1.MembershipType FROM Payments P1
                                             WHERE P1.MemberID = M.MemberID
                                               AND Date(P1.NewExpiryDate) >= Date('now')
                                               AND IFNULL(P1.PaymentMode, '') <> 'Refund'
                                               AND IFNULL(P1.PaymentMode, '') <> 'Refunded'
                                               AND IFNULL(P1.MembershipType, '') NOT LIKE '[REFUND]%'
                                               AND IFNULL(P1.MembershipType, '') NOT LIKE '[REFUNDED]%'
                                             ORDER BY P1.PaymentID ASC LIMIT 1)
                                            || '|' ||
                                            CASE 
                                                WHEN COUNT(P.PaymentID) > 1 THEN '+' || (COUNT(P.PaymentID) - 1)
                                                ELSE '' 
                                            END
                                        FROM Payments P
                                        WHERE P.MemberID = M.MemberID
                                          AND Date(P.NewExpiryDate) >= Date('now')
                                          AND IFNULL(P.PaymentMode, '') <> 'Refund'
                                          AND IFNULL(P.PaymentMode, '') <> 'Refunded'
                                          AND IFNULL(P.MembershipType, '') NOT LIKE '[REFUND]%'
                                          AND IFNULL(P.MembershipType, '') NOT LIKE '[REFUNDED]%'
                                    ), '-'
                                ) as ActivePlans,

                                COALESCE(
                                    (
                                        SELECT Date(P2.NewExpiryDate)
                                        FROM Payments P2
                                        WHERE P2.MemberID = M.MemberID 
                                          AND Date(P2.NewExpiryDate) >= Date('now')
                                          AND IFNULL(P2.PaymentMode, '') <> 'Refunded'
                                          AND IFNULL(P2.PaymentMode, '') <> 'Refund'
                                          AND IFNULL(P2.MembershipType, '') NOT LIKE '[REFUND]%'
                                          AND IFNULL(P2.MembershipType, '') NOT LIKE '[REFUNDED]%'
                                        ORDER BY P2.PaymentID ASC
                                        LIMIT 1
                                    ), '-'
                                ) as CurrentPlanExpiry,

                                CASE 
                                    WHEN I.InstructorID IS NULL THEN '-' 
                                    WHEN I.MiddleInitial IS NULL OR TRIM(I.MiddleInitial) = '' THEN I.FirstName || ' ' || I.LastName 
                                    ELSE I.FirstName || ' ' || I.MiddleInitial || '. ' || I.LastName 
                                END as AssignedTrainerName

                                FROM Members M 
                                LEFT JOIN Instructors I ON M.AssignedInstructorID = I.InstructorID
                                ORDER BY M.MemberID DESC";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // 1. Read the static database status text string
                                string dbStatus = reader["Status"]?.ToString() ?? "Pending";

                                var member = new Member
                                {
                                    MemberID = reader["MemberID"]?.ToString() ?? "",
                                    FirstName = reader["FirstName"]?.ToString() ?? "",
                                    MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                                    LastName = reader["LastName"]?.ToString() ?? "",
                                    Phone = reader["Phone"]?.ToString() ?? "",
                                    Gender = reader["Gender"]?.ToString() ?? "",
                                    MembershipPlan = reader["ActivePlans"]?.ToString() ?? "-",
                                    PhotoPath = reader["PhotoPath"]?.ToString() ?? "",
                                    AssignedInstructorID = reader["AssignedInstructorID"]?.ToString() ?? "",
                                    AssignedTrainerName = reader["AssignedTrainerName"]?.ToString() ?? "-",
                                    DiscountCode = reader["DiscountCode"]?.ToString() ?? "",
                                    StudentExpiryDate = reader["StudentExpiryDate"]?.ToString() ?? ""
                                };

                                if (reader["DateJoined"] != DBNull.Value && DateTime.TryParse(reader["DateJoined"].ToString(), out DateTime joinDate))
                                {
                                    member.DateJoined = joinDate.ToString("yyyy-MM-dd");
                                }
                                else
                                {
                                    member.DateJoined = DateTime.Now.ToString("yyyy-MM-dd");
                                }

                                // 2. Extract and handle the target plan boundary date
                                string dynamicExpiry = reader["CurrentPlanExpiry"]?.ToString() ?? "-";
                                if (dynamicExpiry != "-" && DateTime.TryParse(dynamicExpiry, out DateTime expiryDate))
                                {
                                    member.ExpiryDate = expiryDate.ToString("MM-dd-yyyy");

                                    // ── NEW FIX: RE-EVALUATE STATUS BASED ON NEW DEVICE DATE ──
                                    // Since gym logic tracking runs until the calendar day turns over,
                                    // we mark it expired if the device date is strictly greater than the expiry threshold.
                                    if (DateTime.Today > expiryDate.Date)
                                    {
                                        dbStatus = "Expired";
                                    }
                                    else if (dbStatus == "Expired")
                                    {
                                        dbStatus = "Active";
                                    }
                                }
                                else
                                {
                                    member.ExpiryDate = "-";
                                    // If they have no valid active plans left in their pipeline string, 
                                    // fallback to expired tracking automatically if it was previously active
                                    if (dbStatus == "Active")
                                    {
                                        dbStatus = "Expired";
                                    }
                                }

                                member.Status = dbStatus;

                                if (member.Status == "Expired")
                                    member.MembershipPlan = "-";

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

                // Count active, pending, and expired members dynamically
                int activeCount = 0;
                int pendingCount = 0;
                int expiredCount = 0;
                foreach (var m in MembersList)
                {
                    if (m.Status != null && m.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                    {
                        activeCount++;
                    }
                    else if (m.Status != null && m.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        pendingCount++;
                    }
                    else if (m.Status != null && m.Status.Equals("Expired", StringComparison.OrdinalIgnoreCase))
                    {
                        expiredCount++;
                    }
                }
                lblActiveCount.Text = activeCount.ToString();
                lblPendingCount.Text = pendingCount.ToString();
                lblExpiredCount.Text = expiredCount.ToString();

                _membersView = System.Windows.Data.CollectionViewSource.GetDefaultView(MembersList);
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

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            popFilter.IsOpen = !popFilter.IsOpen;
        }

        private void FilterCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateFilterChips();
            _membersView?.Refresh();
        }

        private FrameworkElement CreateChip(string text, CheckBox associatedCheckBox)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1a1c")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff6b00")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 8, 4),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            stackPanel.Children.Add(textBlock);

            var closeBtn = new Button
            {
                Content = "✕",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff6b00")),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Padding = new Thickness(2, 0, 2, 0)
            };

            closeBtn.Template = new ControlTemplate(typeof(Button))
            {
                VisualTree = new FrameworkElementFactory(typeof(ContentPresenter))
            };

            closeBtn.MouseEnter += (s, e) => closeBtn.Foreground = Brushes.Red;
            closeBtn.MouseLeave += (s, e) => closeBtn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff6b00"));

            closeBtn.Click += (s, e) =>
            {
                associatedCheckBox.IsChecked = false;
            };

            stackPanel.Children.Add(closeBtn);
            border.Child = stackPanel;

            return border;
        }

        private void UpdateFilterChips()
        {
            wpChips.Children.Clear();

            // Status category
            if (chkStatusActive.IsChecked == true) wpChips.Children.Add(CreateChip("Active", chkStatusActive));
            if (chkStatusPending.IsChecked == true) wpChips.Children.Add(CreateChip("Pending", chkStatusPending));
            if (chkStatusExpired.IsChecked == true) wpChips.Children.Add(CreateChip("Expired", chkStatusExpired));

            // Member Type category
            if (chkTypeRegular.IsChecked == true) wpChips.Children.Add(CreateChip("Regular", chkTypeRegular));
            if (chkTypeStudent.IsChecked == true) wpChips.Children.Add(CreateChip("Student", chkTypeStudent));

            // Dynamic types
            foreach (var chk in _dynamicTypeCheckBoxes)
            {
                if (chk.IsChecked == true)
                {
                    wpChips.Children.Add(CreateChip(chk.Content?.ToString() ?? "Type", chk));
                }
            }

            // Dynamic plans
            foreach (var chk in _dynamicPlanCheckBoxes)
            {
                if (chk.IsChecked == true)
                {
                    wpChips.Children.Add(CreateChip(chk.Content?.ToString() ?? "Plan", chk));
                }
            }

            // Plan category
            if (chkPlanNone.IsChecked == true) wpChips.Children.Add(CreateChip("None", chkPlanNone));
        }

        private bool MemberFilterLogic(object obj)
        {
            if (obj is Member member)
            {
                // 1. Search Text (AND)
                string searchText = txtSearch.Text.Trim().ToLower();
                if (!string.IsNullOrEmpty(searchText))
                {
                    bool matchesSearch = member.FullName.ToLower().Contains(searchText) ||
                                         member.MemberID.ToLower().Contains(searchText);
                    if (!matchesSearch) return false;
                }

                // 2. Status Category (AND across categories, OR within category)
                bool isStatusFilterActive = chkStatusActive.IsChecked == true ||
                                            chkStatusPending.IsChecked == true ||
                                            chkStatusExpired.IsChecked == true;
                if (isStatusFilterActive)
                {
                    bool statusMatched = false;
                    if (chkStatusActive.IsChecked == true && member.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)) statusMatched = true;
                    if (chkStatusPending.IsChecked == true && member.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)) statusMatched = true;
                    if (chkStatusExpired.IsChecked == true && member.Status.Equals("Expired", StringComparison.OrdinalIgnoreCase)) statusMatched = true;
                    
                    if (!statusMatched) return false;
                }

                // 3. Member Type Category (AND across categories, OR within category)
                bool isTypeFilterActive = chkTypeRegular.IsChecked == true ||
                                          chkTypeStudent.IsChecked == true;
                foreach (var chk in _dynamicTypeCheckBoxes)
                {
                    if (chk.IsChecked == true)
                    {
                        isTypeFilterActive = true;
                        break;
                    }
                }

                if (isTypeFilterActive)
                {
                    bool typeMatched = false;
                    if (chkTypeRegular.IsChecked == true && member.MemberType.Equals("Regular", StringComparison.OrdinalIgnoreCase)) typeMatched = true;
                    if (chkTypeStudent.IsChecked == true && member.MemberType.Equals("Student", StringComparison.OrdinalIgnoreCase)) typeMatched = true;
                    
                    foreach (var chk in _dynamicTypeCheckBoxes)
                    {
                        if (chk.IsChecked == true && member.MemberType.Equals(chk.Content?.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            typeMatched = true;
                            break;
                        }
                    }

                    if (!typeMatched) return false;
                }

                // 4. Plan Category (AND across categories, OR within category)
                bool isPlanFilterActive = chkPlanNone.IsChecked == true;
                foreach (var chk in _dynamicPlanCheckBoxes)
                {
                    if (chk.IsChecked == true)
                    {
                        isPlanFilterActive = true;
                        break;
                    }
                }

                if (isPlanFilterActive)
                {
                    bool planMatched = false;
                    string planStr = member.MembershipPlan ?? "-";
                    bool hasNoActivePlan = planStr == "-" || string.IsNullOrWhiteSpace(planStr) || member.Status.Equals("Expired", StringComparison.OrdinalIgnoreCase);

                    if (!hasNoActivePlan)
                    {
                        foreach (var chk in _dynamicPlanCheckBoxes)
                        {
                            if (chk.IsChecked == true)
                            {
                                string planName = chk.Content?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(planName) && planStr.Contains(planName, StringComparison.OrdinalIgnoreCase))
                                {
                                    planMatched = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (chkPlanNone.IsChecked == true && hasNoActivePlan)
                    {
                        planMatched = true;
                    }

                    if (!planMatched) return false;
                }

                return true;
            }
            return false;
        }

        private void PayMember_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is Member member)
                PaymentNavigationHelper.TryNavigateToPayment(this, member.MemberID);
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
                    AssignedInstructorID = memberToEdit.AssignedInstructorID,
                    DiscountCode = memberToEdit.DiscountCode,
                    StudentExpiryDate = memberToEdit.StudentExpiryDate,
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

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDynamicMemberTypes();
            LoadDynamicPlans();
            LoadMembers();
        }

        private void QueueBadge_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Member memberObj)
            {
                // Intercept profile checks to handle placeholder values gracefully
                if (string.IsNullOrEmpty(memberObj.MemberID) || memberObj.MembershipPlan == "-") return;

                SubscriptionPipelineWindow pipelineWin = new SubscriptionPipelineWindow(memberObj, this);
                pipelineWin.Owner = Window.GetWindow(this);
                pipelineWin.ShowDialog();
            }
        }

        private void MemberName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Member member)
            {
                var detailsWin = new MemberDetailsWindow(member);
                detailsWin.Owner = Window.GetWindow(this);
                detailsWin.ShowDialog();
            }
        }
    }
}
