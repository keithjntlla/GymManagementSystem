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
        private double basePlanPrice = 0;
        private double totalAmount = 0;
        private string selectedMembershipType = "";
        private int selectedDurationDays = 0;
        private int durationMultiplier = 1;
        private double activeDiscountPercentage = 0;
        private double totalDiscountDeduction = 0;
        private bool isAdvancePaymentMode = false;

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

                durationMultiplier = 1;
                lblMultiplierValue.Text = durationMultiplier.ToString();

                RecalculateFinancialsAndDates();
            }
        }

        private void BtnMultiplier_Increment_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedMembershipType)) return;
            durationMultiplier++;
            lblMultiplierValue.Text = durationMultiplier.ToString();
            RecalculateFinancialsAndDates();
        }

        private void BtnMultiplier_Decrement_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedMembershipType) || durationMultiplier <= 1) return;
            durationMultiplier--;
            lblMultiplierValue.Text = durationMultiplier.ToString();
            RecalculateFinancialsAndDates();
        }

        private void RecalculateFinancialsAndDates()
        {
            double subtotal = basePlanPrice * durationMultiplier;
            activeDiscountPercentage = 0;

            if (selectedMember != null && !string.IsNullOrEmpty(selectedMembershipType))
            {
                var (fixedPct, fixedScope) = DatabaseHelper.GetFixedDiscountConfig(selectedMember.MemberType);

                if (fixedPct > 0)
                {
                    if (fixedScope.Equals("All", StringComparison.OrdinalIgnoreCase) ||
                        fixedScope.ToLower().Contains(selectedMembershipType.ToLower()))
                    {
                        activeDiscountPercentage += fixedPct;
                    }
                }
            }

            totalDiscountDeduction = subtotal * (activeDiscountPercentage / 100.0);
            totalAmount = subtotal - totalDiscountDeduction;

            lblTotalAmount.Text = $"₱{totalAmount:N2}";

            if (activeDiscountPercentage > 0)
            {
                brdDiscountBadge.Visibility = Visibility.Visible;
                lblOriginalSubtotal.Visibility = Visibility.Visible;
                lblDiscountDeductionDisplay.Visibility = Visibility.Visible;

                lblDiscountBadgeText.Text = $"{activeDiscountPercentage}% OFF";
                lblOriginalSubtotal.Text = $"₱{subtotal:N2}";
                lblDiscountDeductionDisplay.Text = $"-₱{totalDiscountDeduction:N2} discount applied";
            }
            else
            {
                brdDiscountBadge.Visibility = Visibility.Collapsed;
                lblOriginalSubtotal.Visibility = Visibility.Collapsed;
                lblDiscountDeductionDisplay.Visibility = Visibility.Collapsed;
            }

            CalculateChange();
            CalculateNewExpiry();
        }

        private void CalculateNewExpiry()
        {
            if (selectedDurationDays == 0) return;

            DateTime baseDate = DateTime.Today;

            if (isAdvancePaymentMode && selectedMember != null &&
                !string.IsNullOrEmpty(selectedMember.ExpiryDate) && selectedMember.ExpiryDate != "-")
            {
                if (DateTime.TryParse(selectedMember.ExpiryDate, out DateTime parsedActiveExpiry))
                {
                    baseDate = parsedActiveExpiry;
                }
            }

            DateTime newExpiry;
            int totalDaysToAdd = selectedDurationDays * durationMultiplier;

            if (selectedMembershipType.Equals("Daily", StringComparison.OrdinalIgnoreCase))
            {
                if (isAdvancePaymentMode)
                    newExpiry = baseDate.AddDays(totalDaysToAdd);
                else
                    newExpiry = baseDate.AddDays(totalDaysToAdd - 1);
            }
            else
            {
                newExpiry = baseDate.AddDays(totalDaysToAdd);
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

                    // ── FIXED SQL QUERY: PULLED DYNAMIC PLANNAME AND MULTIPLIER TEXT FOR SEARCH BAR AS WELL ──
                    string sql = @"
                        SELECT M.*, 
                        COALESCE(
                            (
                                SELECT CASE 
                                    WHEN INSTR(P.MembershipType, ' (') > 0 
                                    THEN SUBSTR(P.MembershipType, 1, INSTR(P.MembershipType, ' (') - 1)
                                    WHEN INSTR(P.MembershipType, '[Advanced] ') > 0
                                    THEN SUBSTR(P.MembershipType, 19)
                                    ELSE P.MembershipType 
                                END
                                FROM Payments P 
                                WHERE P.MemberID = M.MemberID 
                                ORDER BY P.PaymentID DESC 
                                LIMIT 1
                            ), '-'
                        ) as PlanName,
                        COALESCE(
                            (
                                SELECT CASE 
                                    WHEN INSTR(P.MembershipType, ' (') > 0 
                                    THEN 'x' || SUBSTR(P.MembershipType, INSTR(P.MembershipType, ' (') + 2, INSTR(P.MembershipType, ')') - INSTR(P.MembershipType, ' (') - 2)
                                    ELSE '' 
                                END
                                FROM Payments P 
                                WHERE P.MemberID = M.MemberID 
                                ORDER BY P.PaymentID DESC 
                                LIMIT 1
                            ), ''
                        ) as MultiplierText
                        FROM Members M 
                        WHERE M.FullName LIKE @query OR M.MemberID LIKE @query";

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

                                // ── PACK THE SEARCH RESULT STRINGS INTO THE PIPED FIELD FOR THE LOADER ENGINE ──
                                string basePlan = reader["PlanName"]?.ToString() ?? "-";
                                string multiplier = reader["MultiplierText"]?.ToString() ?? "";

                                if (!string.IsNullOrEmpty(multiplier) && basePlan != "-")
                                {
                                    m.MembershipPlan = $"{basePlan}|{multiplier}";
                                }
                                else
                                {
                                    m.MembershipPlan = basePlan;
                                }

                                if (reader["Birthday"] != DBNull.Value && DateTime.TryParse(reader["Birthday"].ToString(), out DateTime bDay))
                                {
                                    m.Birthday = bDay;
                                }

                                m.MemberType = reader["MemberType"] != DBNull.Value
                                    ? reader["MemberType"].ToString() ?? "Regular"
                                    : "Regular";

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
                var result = MessageBox.Show(
                    $"Member {member.FullName} is still Active (Expires: {member.ExpiryDate}).\n\n" +
                    "Would you like to process an Advance Payment? This will extend their current plan without losing remaining days.",
                    "Active Membership Detected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    isAdvancePaymentMode = true;
                    lblExpiryTitle.Text = "Extended Expiry Date";
                    btnProcessPayment.IsEnabled = true;
                    btnProcessPayment.Opacity = 1.0;
                }
                else
                {
                    isAdvancePaymentMode = false;
                    lblExpiryTitle.Text = "New Expiry Date";
                    btnProcessPayment.IsEnabled = false;
                    btnProcessPayment.Opacity = 0.5;
                }
            }
            else
            {
                isAdvancePaymentMode = false;
                lblExpiryTitle.Text = "New Expiry Date";
                btnProcessPayment.IsEnabled = true;
                btnProcessPayment.Opacity = 1.0;
            }

            RecalculateFinancialsAndDates();
        }

        private void DisplayMemberInfo(Member member)
        {
            panelNoMember.Visibility = Visibility.Collapsed;
            panelMemberInfo.Visibility = Visibility.Visible;

            lblMemberName.Text = member.FullName;
            lblMemberID.Text = member.MemberID;
            lblMemberPhone.Text = member.Phone;
            lblMemberGender.Text = member.Gender;

            if (member.Birthday.HasValue)
            {
                lblMemberBirthday.Text = member.Birthday.Value.ToString("yyyy-MM-dd");
            }
            else
            {
                lblMemberBirthday.Text = "N/A";
            }

            lblMemberType.Text = member.MemberType.ToString();

            // ── FIXED: EXTRACT BASE PLAN AND MULTIPLIER SEPARATELY FOR DISPLAY ──
            if (!string.IsNullOrWhiteSpace(member.MembershipPlan) && member.MembershipPlan != "-")
            {
                // Check if the plan data contains our custom layout pipe character
                if (member.MembershipPlan.Contains("|"))
                {
                    string[] parts = member.MembershipPlan.Split('|');
                    string basePlan = parts[0];
                    string multiplier = parts[1];

                    // Render both beautifully together (e.g., "Daily (x3)")
                    lblMemberPlan.Text = $"{basePlan} ({multiplier})";
                }
                else
                {
                    lblMemberPlan.Text = member.MembershipPlan;
                }
            }
            else
            {
                lblMemberPlan.Text = "-";
            }

            // Bind current active timeline expiry milestone parameter
            if (!string.IsNullOrWhiteSpace(member.ExpiryDate) && member.ExpiryDate != "-")
            {
                lblCurrentExpiry.Text = member.ExpiryDate;
            }
            else
            {
                lblCurrentExpiry.Text = "-";
            }

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

            string formattedPlanDescription = durationMultiplier > 1
                ? $"{selectedMembershipType} ({durationMultiplier})"
                : selectedMembershipType;

            string paymentPrefix = isAdvancePaymentMode ? "[Advanced] " : "";

            string summary = $"Please confirm the following payment:\n\n" +
                             $"  Member: {selectedMember.FullName}\n" +
                             $"  Plan: {paymentPrefix}{formattedPlanDescription}\n" +
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
                        string paySql = @"INSERT INTO Payments (MemberID, MemberName, AmountPaid, TotalAmount, Change, PaymentMode, MembershipType, DateOfTransaction, NewExpiryDate, DiscountAmount) 
                                VALUES (@id, @mname, @paid, @total, @change, @mode, @type, @date, @expiry, @discountAmount)";
                        using (var cmd = new SQLiteCommand(paySql, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", selectedMember.MemberID);
                            cmd.Parameters.AddWithValue("@mname", selectedMember.FullName);
                            cmd.Parameters.AddWithValue("@paid", paid);
                            cmd.Parameters.AddWithValue("@total", totalAmount);
                            cmd.Parameters.AddWithValue("@change", change);
                            cmd.Parameters.AddWithValue("@discountAmount", totalDiscountDeduction);
                            cmd.Parameters.AddWithValue("@mode", paymentMode);
                            cmd.Parameters.AddWithValue("@type", paymentPrefix + formattedPlanDescription);
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
            isAdvancePaymentMode = false;

            lblMemberPlan.Text = "-";
            lblCurrentExpiry.Text = "-";

            lblExpiryTitle.Text = "New Expiry Date";
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

            brdDiscountBadge.Visibility = Visibility.Collapsed;
            lblOriginalSubtotal.Visibility = Visibility.Collapsed;
            lblDiscountDeductionDisplay.Visibility = Visibility.Collapsed;
        }
    }
}