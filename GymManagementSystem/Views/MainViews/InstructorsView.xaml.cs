using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Views.Windows;

namespace GymManagementSystem.Views.MainViews
{
    public partial class InstructorsView : UserControl
    {
        public ObservableCollection<Instructor> InstructorsList { get; set; } = new ObservableCollection<Instructor>();
        private ICollectionView? _instructorsView;

        public InstructorsView()
        {
            InitializeComponent();
            LoadInstructors();
        }

        public void LoadInstructors()
        {
            InstructorsList.Clear();
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT I.*, 
                        (SELECT COUNT(*) FROM Members M WHERE M.AssignedInstructorID = I.InstructorID) as ClientCount
                        FROM Instructors I 
                        ORDER BY I.InstructorID DESC";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var instructor = new Instructor
                            {
                                InstructorID = reader["InstructorID"]?.ToString() ?? "",
                                FirstName = reader["FirstName"]?.ToString() ?? "",
                                MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                                LastName = reader["LastName"]?.ToString() ?? "",
                                Phone = reader["Phone"]?.ToString() ?? "",
                                Specialization = reader["Specialization"]?.ToString() ?? "",
                                Status = reader["Status"]?.ToString() ?? "Active",
                                PhotoPath = reader["PhotoPath"]?.ToString() ?? "",
                                DateHired = reader["DateHired"]?.ToString() ?? "",
                                Birthday = reader["Birthday"] != DBNull.Value && reader["Birthday"] != null && DateTime.TryParse(reader["Birthday"].ToString(), out DateTime bday) ? (DateTime?)bday : null,
                                Gender = reader["Gender"]?.ToString() ?? "",
                                ClientCount = Convert.ToInt32(reader["ClientCount"])
                            };
                            InstructorsList.Add(instructor);
                        }
                    }
                }

                // Update cards
                int totalCount = InstructorsList.Count;
                int activeCount = 0;
                int inactiveCount = 0;

                foreach (var inst in InstructorsList)
                {
                    if (inst.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                    {
                        activeCount++;
                    }
                    else
                    {
                        inactiveCount++;
                    }
                }

                lblTotalInstructorsCount.Text = totalCount.ToString();
                lblActiveInstructorsCount.Text = activeCount.ToString();
                lblInactiveInstructorsCount.Text = inactiveCount.ToString();

                _instructorsView = CollectionViewSource.GetDefaultView(InstructorsList);
                _instructorsView.Filter = InstructorFilterLogic;
                dgInstructors.ItemsSource = InstructorsList;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading instructors: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool InstructorFilterLogic(object obj)
        {
            if (obj is Instructor inst)
            {
                // Search Text
                string searchText = txtSearch.Text.Trim().ToLower();
                if (!string.IsNullOrEmpty(searchText))
                {
                    bool matchesSearch = inst.FullName.ToLower().Contains(searchText) ||
                                         inst.InstructorID.ToLower().Contains(searchText) ||
                                         inst.Specialization.ToLower().Contains(searchText);
                    if (!matchesSearch) return false;
                }

                // Status dropdown
                if (cmbStatusFilter.SelectedItem is ComboBoxItem selectedItem)
                {
                    string statusFilter = selectedItem.Content?.ToString() ?? "All Status";
                    if (statusFilter != "All Status")
                    {
                        if (!inst.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            return false;
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            _instructorsView?.Refresh();
        }

        private void AddInstructor_Click(object sender, RoutedEventArgs e)
        {
            AddInstructorWindow addWin = new AddInstructorWindow();
            addWin.Owner = Window.GetWindow(this);
            if (addWin.ShowDialog() == true)
            {
                LoadInstructors();
            }
        }

        private void EditInstructor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Instructor instructor)
            {
                AddInstructorWindow editWin = new AddInstructorWindow(instructor);
                editWin.Owner = Window.GetWindow(this);
                if (editWin.ShowDialog() == true)
                {
                    LoadInstructors();
                }
            }
        }

        private void DeleteInstructor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Instructor instructor)
            {
                if (instructor.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Cannot delete active instructors. Please change their status to Inactive first.", 
                                    "Action Denied", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }

                if (instructor.ClientCount > 0)
                {
                    MessageBox.Show($"Cannot delete this instructor because they currently have {instructor.ClientCount} client(s) assigned to them.\n\n" +
                                    "Please re-assign or unassign their clients first.",
                                    "Action Denied", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }

                var result = MessageBox.Show($"Are you sure you want to permanently delete instructor {instructor.FullName}?\n\nThis action cannot be undone.", 
                                             "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                        {
                            conn.Open();
                            string sql = "DELETE FROM Instructors WHERE InstructorID = @id";
                            using (var cmd = new SQLiteCommand(sql, conn))
                            {
                                cmd.Parameters.AddWithValue("@id", instructor.InstructorID);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        if (!string.IsNullOrEmpty(instructor.PhotoPath) && File.Exists(instructor.PhotoPath))
                        {
                            try { File.Delete(instructor.PhotoPath); } catch { }
                        }

                        LoadInstructors();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error deleting instructor: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ClientsCount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Instructor instructor)
            {
                InstructorClientsWindow clientsWin = new InstructorClientsWindow(instructor);
                clientsWin.Owner = Window.GetWindow(this);
                clientsWin.ShowDialog();
            }
        }

        private void InstructorName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Instructor instructor)
            {
                InstructorDetailsWindow detailsWin = new InstructorDetailsWindow(instructor);
                detailsWin.Owner = Window.GetWindow(this);
                if (detailsWin.ShowDialog() == true)
                {
                    LoadInstructors();
                }
            }
        }
    }
}
