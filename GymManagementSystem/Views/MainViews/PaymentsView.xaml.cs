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
        private double totalAmount = 0;
        private string selectedMembershipType = "";
        private int selectedDurationDays = 0;

        public PaymentsView()
        {
            InitializeComponent();
            lblTransactionDate.Text = DateTime.Now.ToString("M/d/yyyy");
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
                totalAmount = plan.Price;
                selectedMembershipType = plan.PlanName;
                selectedDurationDays = plan.DurationDays;

                lblTotalAmount.Text = $"₱{totalAmount:N2}";
                CalculateChange();
                CalculateNewExpiry();
            }
        }

        private void CalculateNewExpiry()
        {
            if (selectedDurationDays == 0) return;

            DateTime newExpiry;
            // Logic Fix: For 'Daily' memberships, the expiry is the same calendar day.
            // Using DateTime.Today ensures no timestamp issues and alignment with SQL Date() functions.
            if (selectedMembershipType.Equals("Daily", StringComparison.OrdinalIgnoreCase))
            {
                newExpiry = DateTime.Today;
            }
            else
            {
                newExpiry = DateTime.Today.AddDays(selectedDurationDays);
            }
            lblNewExpiryDate.Text = newExpiry.ToString("M/d/yyyy"); ;
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
                                results.Add(new Member
                                {
                                    MemberID = reader["MemberID"]?.ToString() ?? "",
                                    FullName = reader["FullName"]?.ToString() ?? "",
                                    Phone = reader["Phone"]?.ToString() ?? "",
                                    Gender = reader["Gender"]?.ToString() ?? "",
                                    PhotoPath = reader["PhotoPath"]?.ToString() ?? "",
                                    Status = reader["Status"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"]?.ToString() ?? ""
                                });
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
            string summary = $"Please confirm the following payment:\n\n" +
                             $"  Member: {selectedMember.FullName}\n" +
                             $"  Plan: {selectedMembershipType}\n" +
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
                            cmd.Parameters.AddWithValue("@type", selectedMembershipType);
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
            totalAmount = 0;
            selectedMembershipType = "";
            selectedDurationDays = 0;
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