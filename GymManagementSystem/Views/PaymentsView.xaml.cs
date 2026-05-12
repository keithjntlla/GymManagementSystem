using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Data.SQLite;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using System.IO;

namespace GymManagementSystem
{
    public partial class PaymentsView : UserControl
    {
        private Member? selectedMember;
        private double totalAmount = 0;
        private string selectedMembershipType = "";

        public PaymentsView()
        {
            InitializeComponent();
            lblTransactionDate.Text = DateTime.Now.ToString("M/d/yyyy");
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
                try
                {
                    imgMemberPhoto.Source = new BitmapImage(new Uri(member.PhotoPath));
                }
                catch
                {
                    imgMemberPhoto.Source = null;
                }
            }
            else
            {
                imgMemberPhoto.Source = null;
            }
        }

        private void Rate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                string[] parts = tag.Split('|');
                if (parts.Length == 2)
                {
                    totalAmount = double.Parse(parts[0]);
                    selectedMembershipType = parts[1];
                    lblTotalAmount.Text = $"₱{totalAmount:N2}";
                    CalculateChange();
                    CalculateNewExpiry();
                }
            }
        }

        private void TxtAmountPaid_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(txtAmountPaid.Text, out double paid))
            {
                lblAmountPaidDisplay.Text = $"₱{paid:N2}";
            }
            else
            {
                lblAmountPaidDisplay.Text = "₱0.00";
            }
            CalculateChange();
        }

        private void CalculateChange()
        {
            if (double.TryParse(txtAmountPaid.Text, out double paid))
            {
                double change = paid - totalAmount;
                lblChange.Text = $"₱{(change > 0 ? change : 0):N2}";
            }
            else
            {
                lblChange.Text = "₱0.00";
            }
        }

        private void CalculateNewExpiry()
        {
            if (string.IsNullOrEmpty(selectedMembershipType)) return;

            DateTime newExpiry = DateTime.Now;
            switch (selectedMembershipType)
            {
                case "Daily": newExpiry = DateTime.Now.AddDays(1); break;
                case "Weekly": newExpiry = DateTime.Now.AddDays(7); break;
                case "Half-Month": newExpiry = DateTime.Now.AddDays(15); break;
                case "Monthly": newExpiry = DateTime.Now.AddMonths(1); break;
                case "Yearly": newExpiry = DateTime.Now.AddYears(1); break;
            }
            lblNewExpiryDate.Text = newExpiry.ToString("yyyy-MM-dd");
        }

        private void ProcessPayment_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMember == null)
            {
                MessageBox.Show("Please select a member first.");
                return;
            }

            if (totalAmount <= 0)
            {
                MessageBox.Show("Please select a membership rate.");
                return;
            }

            if (!double.TryParse(txtAmountPaid.Text, out double paid) || paid < totalAmount)
            {
                MessageBox.Show("Insufficient amount paid.");
                return;
            }

            if (selectedMember.Status == "Active")
            {
                if (DateTime.TryParse(selectedMember.ExpiryDate, out DateTime expiryDate))
                {
                    if (expiryDate > DateTime.Now)
                    {
                        MessageBox.Show("This member is already active with a valid membership. They cannot pay again until their membership expires.", "Payment Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }

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
                            cmd.Parameters.AddWithValue("@mode", rbCash.IsChecked == true ? "Cash" : "GCash");
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
            txtSearch.Clear();
            txtAmountPaid.Clear();
            lblTotalAmount.Text = "₱0.00";
            lblAmountPaidDisplay.Text = "₱0.00";
            lblChange.Text = "₱0.00";
            lblNewExpiryDate.Text = "-";
            panelMemberInfo.Visibility = Visibility.Collapsed;
            panelNoMember.Visibility = Visibility.Visible;
        }
    }
}