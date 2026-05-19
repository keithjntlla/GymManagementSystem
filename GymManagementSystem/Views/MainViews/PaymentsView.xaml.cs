using GymManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace GymManagementSystem.Views.MainViews
{
    public partial class PaymentsView : UserControl
    {
        private Member? selectedMember;
        private double basePlanPrice = 0; // Tracks the single item rate cost
        private double totalAmount = 0;   // basePlanPrice * durationMultiplier
        private string selectedMembershipType = "";
        private int selectedDurationDays = 0;
        private int durationMultiplier = 1; // Default multiplier

        public PaymentsView()
        {
            InitializeComponent();
            lblTransactionDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
            LoadDynamicRates();
        }

        public PaymentsView(Member member) : this()
        {
            selectedMember = member;
            txtSearch.Text = member.FullName;
            DisplayMemberInfo(member);
            CheckIfMemberIsActive(member);
        }

        private void LoadDynamicRates()
        {
            List<GymPlan> plans = new List<GymPlan>();
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT * FROM Rates WHERE IsArchived = 0";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            plans.Add(new GymPlan
                            {
                                PlanName = reader["PlanName"].ToString() ?? "",
                                Price = Convert.ToDouble(reader["Price"]),
                                DurationDays = Convert.ToInt32(reader["DurationDays"])
                            });
                        }
                    }
                }
                icRates.ItemsSource = plans;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading rates: " + ex.Message);
            }
        }

        private void Rate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is GymPlan plan)
            {
                basePlanPrice = plan.Price;
                selectedMembershipType = plan.PlanName;
                selectedDurationDays = plan.DurationDays;

                // Reset multiplier back to 1 on plan selection switch
                durationMultiplier = 1;
                lblMultiplierValue.Text = durationMultiplier.ToString();

                RecalculateFinancialsAndDates();
            }
        }

        // NEW: Spinbox/Spinner Button Event Subroutines
        private void BtnMultiplier_Increment_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedMembershipType)) return; // Don't scale if no rate selected
            durationMultiplier++;
            lblMultiplierValue.Text = durationMultiplier.ToString();
            RecalculateFinancialsAndDates();
        }

        private void BtnMultiplier_Decrement_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedMembershipType) || durationMultiplier <= 1) return; // Prevent falling below 1
            durationMultiplier--;
            lblMultiplierValue.Text = durationMultiplier.ToString();
            RecalculateFinancialsAndDates();
        }

        private void RecalculateFinancialsAndDates()
        {
            totalAmount = basePlanPrice * durationMultiplier;
            lblTotalAmount.Text = $"₱{totalAmount:N2}";

            CalculateChange();
            CalculateNewExpiry();
        }

        private void CalculateNewExpiry()
        {
            if (selectedDurationDays == 0) return;

            DateTime newExpiry;
            // Evaluates total scaled days to add accurately (selectedDurationDays * multiplier)
            int totalDaysToAdd = selectedDurationDays * durationMultiplier;

            if (selectedMembershipType.Equals("Daily", StringComparison.OrdinalIgnoreCase))
            {
                // If Daily is multiplied (e.g., multiplier of 3 days), adjust scale calculations cleanly
                newExpiry = DateTime.Today.AddDays(totalDaysToAdd - 1);
            }
            else
            {
                newExpiry = DateTime.Today.AddDays(totalDaysToAdd);
            }
            lblNewExpiryDate.Text = newExpiry.ToString("yyyy-MM-dd");
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = txtSearch.Text.Trim();
            if (query.Length >= 1)
            {
                SearchMembers(query);
            }
            else
            {
                popSearch.IsOpen = false;
            }
        }

        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtSearch.Text.Trim().Length >= 1)
            {
                popSearch.IsOpen = true;
            }
        }

        private void SearchMembers(string query)
        {
            ObservableCollection<Member> results = new ObservableCollection<Member>();
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT * FROM Members WHERE FullName LIKE @query OR MemberID LIKE @query";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@query", "%" + query + "%");
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var m = new Member
                                {
                                    MemberID = reader["MemberID"]?.ToString() ?? "",
                                    FirstName = reader["FirstName"]?.ToString() ?? "",
                                    MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                                    LastName = reader["LastName"]?.ToString() ?? "",
                                    Phone = reader["Phone"]?.ToString() ?? "",
                                    Gender = reader["Gender"]?.ToString() ?? "",
                                    PhotoPath = reader["PhotoPath"]?.ToString() ?? "",
                                    Status = reader["Status"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"]?.ToString() ?? ""
                                };

                                // ADD THESE BLOCKS TO FIX THE DROPDOWN SELECTION BUG
                                if (reader["Birthday"] != DBNull.Value && DateTime.TryParse(reader["Birthday"].ToString(), out DateTime bDay))
                                {
                                    m.Birthday = bDay;
                                }

                                if (reader["MemberType"] != DBNull.Value && Enum.TryParse(reader["MemberType"].ToString(), out MembershipType type))
                                {
                                    m.MemberType = type;
                                }

                                results.Add(m);
                            }
                        }
                    }
                }
                lstSearchResults.ItemsSource = results;
                popSearch.IsOpen = results.Count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Search error: " + ex.Message);
            }
        }

        private void LstSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSearchResults.SelectedItem is Member member)
            {
                selectedMember = member;
                DisplayMemberInfo(member);
                popSearch.IsOpen = false;
                txtSearch.Text = member.FullName;
                CheckIfMemberIsActive(member);
            }
        }

        private void CheckIfMemberIsActive(Member member)
        {
            if (member.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show($"Member {member.FullName} is still Active. You cannot process a new payment until their current membership expires.",
                                "Member Still Active", MessageBoxButton.OK, MessageBoxImage.Warning);

                btnProcessPayment.IsEnabled = false;
                btnProcessPayment.Opacity = 0.5;
            }
            else
            {
                btnProcessPayment.IsEnabled = true;
                btnProcessPayment.Opacity = 1.0;
            }
        }

        private void DisplayMemberInfo(Member member)
        {
            panelNoMember.Visibility = Visibility.Collapsed;
            panelMemberInfo.Visibility = Visibility.Visible;

            lblMemberName.Text = member.FullName;
            lblMemberID.Text = member.MemberID;
            lblMemberPhone.Text = member.Phone;
            lblMemberGender.Text = member.Gender;

            // NEW: Assign Birthday formatted cleanly to yyyy-MM-dd
            if (member.Birthday.HasValue)
            {
                lblMemberBirthday.Text = member.Birthday.Value.ToString("yyyy-MM-dd");
            }
            else
            {
                lblMemberBirthday.Text = "N/A";
            }

            // NEW: Assign Membership Type Enum value
            lblMemberType.Text = member.MemberType.ToString();

            if (!string.IsNullOrEmpty(member.PhotoPath) && File.Exists(member.PhotoPath))
            {
                try { imgMemberPhoto.Source = new BitmapImage(new Uri(member.PhotoPath)); }
                catch { imgMemberPhoto.Source = null; }
            }
            else { imgMemberPhoto.Source = null; }
        }

        private void TxtAmountPaid_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(txtAmountPaid.Text, out double paid))
                lblAmountPaidDisplay.Text = $"₱{paid:N2}";
            else
                lblAmountPaidDisplay.Text = "₱0.00";

            CalculateChange();
        }

        private void CalculateChange()
        {
            if (double.TryParse(txtAmountPaid.Text, out double paid))
            {
                double change = paid - totalAmount;
                lblChange.Text = $"₱{(change > 0 ? change : 0):N2}";
            }
            else { lblChange.Text = "₱0.00"; }
        }

        private void ProcessPayment_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMember == null)
            {
                MessageBox.Show("Please select a member first.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (totalAmount <= 0)
            {
                MessageBox.Show("Please select a membership rate.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(txtAmountPaid.Text, out double paid))
            {
                MessageBox.Show("Please enter a valid amount.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (paid < totalAmount)
            {
                double lacking = totalAmount - paid;

                MessageBox.Show($"Insufficient amount.\n\n" +
                                $"Total: ₱{totalAmount:N2}\n" +
                                $"Paid: ₱{paid:N2}\n" +
                                $"Lacking: ₱{lacking:N2}",
                                "Insufficient Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string paymentMode = rbCash.IsChecked == true ? "Cash" : "GCash";
            double change = paid - totalAmount;

            // Format dynamic descriptions depending on multiplier additions (e.g., "Monthly (x3)")
            string formattedPlanDescription = durationMultiplier > 1
                ? $"{selectedMembershipType} ({durationMultiplier})"
                : selectedMembershipType;

            string summary = $"Please confirm the following payment:\n\n" +
                             $"  Member: {selectedMember.FullName}\n" +
                             $"  Plan: {formattedPlanDescription}\n" +
                             $"  Total: ₱{totalAmount:N2}\n" +
                             $"  Amount Paid: ₱{paid:N2}\n" +
                             $"  Change: ₱{change:N2}\n" +
                             $"  Mode: {paymentMode}\n" +
                             $"  New Expiry: {lblNewExpiryDate.Text}\n\n" +
                             $"Proceed with this payment?";

            var result = MessageBox.Show(summary, "Confirm Payment",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (var trans = conn.BeginTransaction())
                    {
                        string paySql = @"INSERT INTO Payments (MemberID, MemberName, AmountPaid, TotalAmount, Change, PaymentMode, MembershipType, DateOfTransaction, NewExpiryDate) 
                                VALUES (@mid, @mname, @paid, @total, @change, @mode, @type, @date, @expiry)";
                        using (var cmd = new SQLiteCommand(paySql, conn))
                        {
                            cmd.Parameters.AddWithValue("@mid", selectedMember.MemberID);
                            cmd.Parameters.AddWithValue("@mname", selectedMember.FullName);
                            cmd.Parameters.AddWithValue("@paid", paid);
                            cmd.Parameters.AddWithValue("@total", totalAmount);
                            cmd.Parameters.AddWithValue("@change", paid - totalAmount);
                            cmd.Parameters.AddWithValue("@mode", paymentMode);
                            cmd.Parameters.AddWithValue("@type", formattedPlanDescription); // Stores text descriptive variants inside SQLite log history
                            cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@expiry", lblNewExpiryDate.Text);
                            cmd.ExecuteNonQuery();
                        }

                        string memberSql = "UPDATE Members SET Status = 'Active', ExpiryDate = @expiry WHERE MemberID = @mid";
                        using (var cmd = new SQLiteCommand(memberSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@expiry", lblNewExpiryDate.Text);
                            cmd.Parameters.AddWithValue("@mid", selectedMember.MemberID);
                            cmd.ExecuteNonQuery();
                        }
                        trans.Commit();
                    }
                }
                MessageBox.Show("Payment Successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error processing payment: " + ex.Message);
            }
        }

        private void ResetForm()
        {
            selectedMember = null;
            basePlanPrice = 0;
            totalAmount = 0;
            selectedMembershipType = "";
            selectedDurationDays = 0;
            durationMultiplier = 1;

            lblMultiplierValue.Text = "1";
            txtSearch.Clear();
            txtAmountPaid.Clear();
            lblTotalAmount.Text = "₱0.00";
            lblAmountPaidDisplay.Text = "₱0.00";
            lblChange.Text = "₱0.00";
            lblNewExpiryDate.Text = "-";
            panelMemberInfo.Visibility = Visibility.Collapsed;
            panelNoMember.Visibility = Visibility.Visible;
            btnProcessPayment.IsEnabled = true;
            btnProcessPayment.Opacity = 1.0;
        }
    }
}