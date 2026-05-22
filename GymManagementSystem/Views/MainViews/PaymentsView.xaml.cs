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
        private bool isSelectingMemberFromCode = false;
        private List<GymPlan> _ratePlans = new List<GymPlan>();

        private ValidationHelper _validationHelper = null!;

        public PaymentsView()
        {
            InitializeComponent();
            lblTransactionDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
            LoadDynamicRates();
            InitializeValidation();
        }

        public PaymentsView(Member member, bool? advancePaymentConfirmed = null) : this()
        {
            SelectMemberForPayment(member.MemberID, advancePaymentConfirmed);
        }

        private void InitializeValidation()
        {
            _validationHelper = new ValidationHelper();

            _validationHelper.RegisterTextBox(txtSearch, lblSearchError, input =>
            {
                if (selectedMember == null)
                {
                    return (false, input, "Please select a member from the search results.");
                }
                return (true, input, "");
            });

            _validationHelper.RegisterTextBox(txtAmountPaid, lblAmountPaidError, input =>
            {
                if (selectedMember == null)
                {
                    return (false, input, "Please select a member first.");
                }
                if (totalAmount <= 0)
                {
                    return (false, input, "Please select a membership rate.");
                }
                return InputValidator.ValidateAmountPaid(input, totalAmount);
            });
        }

        private void SelectMemberForPayment(string memberId, bool? advancePaymentConfirmed = null)
        {
            Member? member = LoadPaymentMemberProfile(memberId);
            if (member == null)
            {
                MessageBox.Show("Unable to load selected member details.", "Member Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            selectedMember = member;
            _validationHelper.ClearErrors();

            isSelectingMemberFromCode = true;
            txtSearch.Text = member.FullName;
            isSelectingMemberFromCode = false;

            popSearch.IsOpen = false;
            DisplayMemberInfo(member);
            RefreshRateDiscountBadges();

            if (advancePaymentConfirmed.HasValue)
                ApplyPaymentMode(advancePaymentConfirmed.Value);
            else
                CheckIfMemberIsActive(member);
        }

        private void ApplyPaymentMode(bool advancePayment)
        {
            if (advancePayment)
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
                btnProcessPayment.IsEnabled = true;
                btnProcessPayment.Opacity = 1.0;
            }

            RecalculateFinancialsAndDates();
        }

        private Member? LoadPaymentMemberProfile(string memberId)
        {
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
                                  AND IFNULL(P2.PaymentMode, '') <> 'Refund'
                                  AND IFNULL(P2.PaymentMode, '') <> 'Refunded'
                                  AND IFNULL(P2.MembershipType, '') NOT LIKE '[REFUND]%'
                                  AND IFNULL(P2.MembershipType, '') NOT LIKE '[REFUNDED]%'
                                ORDER BY P2.PaymentID ASC
                                LIMIT 1
                            ), '-'
                        ) as CurrentPlanExpiry
                        FROM Members M
                        WHERE M.MemberID = @mid
                        LIMIT 1";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@mid", memberId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read()) return null;

                            string dbStatus = reader["Status"]?.ToString() ?? "Pending";
                            string dynamicExpiry = reader["CurrentPlanExpiry"]?.ToString() ?? "-";

                            var member = new Member
                            {
                                MemberID = reader["MemberID"]?.ToString() ?? "",
                                FirstName = reader["FirstName"]?.ToString() ?? "",
                                MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                                LastName = reader["LastName"]?.ToString() ?? "",
                                Phone = reader["Phone"]?.ToString() ?? "",
                                Gender = reader["Gender"]?.ToString() ?? "",
                                MembershipPlan = NormalizePlanDisplay(reader["ActivePlans"]?.ToString() ?? "-"),
                                PhotoPath = reader["PhotoPath"]?.ToString() ?? "",
                                MemberType = reader["MemberType"] != DBNull.Value
                                    ? reader["MemberType"].ToString() ?? "Regular"
                                    : "Regular"
                            };

                            if (reader["Birthday"] != DBNull.Value &&
                                DateTime.TryParse(reader["Birthday"].ToString(), out DateTime bDay))
                            {
                                member.Birthday = bDay;
                            }

                            if (reader["DateJoined"] != DBNull.Value &&
                                DateTime.TryParse(reader["DateJoined"].ToString(), out DateTime joinDate))
                            {
                                member.DateJoined = joinDate.ToString("yyyy-MM-dd");
                            }

                            if (dynamicExpiry != "-" && DateTime.TryParse(dynamicExpiry, out DateTime expiryDate))
                            {
                                member.ExpiryDate = expiryDate.ToString("yyyy-MM-dd");
                                if (DateTime.Today > expiryDate.Date)
                                {
                                    dbStatus = "Expired";
                                }
                            }
                            else
                            {
                                member.ExpiryDate = "-";
                                if (dbStatus == "Active")
                                {
                                    dbStatus = "Expired";
                                }
                            }

                            member.Status = dbStatus;
                            return member;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading member payment profile: " + ex.Message);
                return null;
            }
        }

        private static string NormalizePlanDisplay(string plan)
        {
            if (string.IsNullOrWhiteSpace(plan) || plan == "-") return "-";
            return plan.EndsWith("|", StringComparison.Ordinal) ? plan.TrimEnd('|') : plan;
        }

        private void LoadDynamicRates()
        {
            _ratePlans.Clear();
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
                            _ratePlans.Add(new GymPlan
                            {
                                PlanName = reader["PlanName"].ToString() ?? "",
                                Price = Convert.ToDouble(reader["Price"]),
                                DurationDays = Convert.ToInt32(reader["DurationDays"])
                            });
                        }
                    }
                }
                RefreshRateDiscountBadges();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading rates: " + ex.Message);
            }
        }

        private static bool IsPlanEligibleForDiscount(string planName, double discountPct, string allowedScope)
        {
            if (discountPct <= 0 || string.IsNullOrWhiteSpace(planName))
                return false;

            if (allowedScope.Equals("All", StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (string scopePart in allowedScope.Split(','))
            {
                string trimmed = scopePart.Trim();
                if (trimmed.Length == 0)
                    continue;

                if (trimmed.Equals(planName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return allowedScope.Contains(planName, StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshRateDiscountBadges()
        {
            double discountPct = 0;
            string allowedScope = "All";

            if (selectedMember != null)
            {
                var (fixedPct, fixedScope) = DatabaseHelper.GetFixedDiscountConfig(selectedMember.MemberType);
                discountPct = fixedPct;
                allowedScope = fixedScope;
            }

            foreach (GymPlan plan in _ratePlans)
            {
                plan.ApplicableDiscountPercentage = IsPlanEligibleForDiscount(plan.PlanName, discountPct, allowedScope)
                    ? discountPct
                    : 0;
            }

            icRates.ItemsSource = null;
            icRates.ItemsSource = _ratePlans;
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

                if (IsPlanEligibleForDiscount(selectedMembershipType, fixedPct, fixedScope))
                    activeDiscountPercentage += fixedPct;
            }

            totalDiscountDeduction = subtotal * (activeDiscountPercentage / 100.0);
            totalAmount = subtotal - totalDiscountDeduction;

            lblTotalAmount.Text = $"₱{totalAmount:N2}";

            if (activeDiscountPercentage > 0)
            {
                lblOriginalSubtotal.Visibility = Visibility.Visible;
                lblDiscountDeductionDisplay.Visibility = Visibility.Visible;
                lblOriginalSubtotal.Text = $"₱{subtotal:N2}";
                lblDiscountDeductionDisplay.Text = $"-₱{totalDiscountDeduction:N2} discount applied";
            }
            else
            {
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
            if (isSelectingMemberFromCode) return;

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
                                  AND IFNULL(P.PaymentMode, '') <> 'Refund'
                                  AND IFNULL(P.PaymentMode, '') <> 'Refunded'
                                  AND IFNULL(P.MembershipType, '') NOT LIKE '[REFUND]%'
                                  AND IFNULL(P.MembershipType, '') NOT LIKE '[REFUNDED]%'
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
                                  AND IFNULL(P.PaymentMode, '') <> 'Refund'
                                  AND IFNULL(P.PaymentMode, '') <> 'Refunded'
                                  AND IFNULL(P.MembershipType, '') NOT LIKE '[REFUND]%'
                                  AND IFNULL(P.MembershipType, '') NOT LIKE '[REFUNDED]%'
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
                SelectMemberForPayment(member.MemberID);
                lstSearchResults.SelectedItem = null;
            }
        }

        private void CheckIfMemberIsActive(Member member)
        {
            if (member == null) return;

            // Check how many future/active plans this member currently has in the database
            int existingCount = 0;
            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string countSql = @"
                        SELECT COUNT(*)
                        FROM Payments
                        WHERE MemberID = @mid
                          AND Date(NewExpiryDate) >= Date('now')
                          AND IFNULL(PaymentMode, '') <> 'Refund'
                          AND IFNULL(PaymentMode, '') <> 'Refunded'
                          AND IFNULL(MembershipType, '') NOT LIKE '[REFUND]%'
                          AND IFNULL(MembershipType, '') NOT LIKE '[REFUNDED]%'";
                    using (var cmd = new SQLiteCommand(countSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@mid", member.MemberID);
                        existingCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error checking pipeline status: " + ex.Message);
            }

            // STRICTOR SAFETY CAP EXFORCEMENT
            // If existingCount >= 2, they already have 1 Active Plan AND 1 Queued Advanced Plan.
            if (existingCount >= 2)
            {
                MessageBox.Show(
                    $"Action Denied: {member.FullName} already has an advanced plan waiting in the pipeline.\n\n" +
                    "You cannot add more than 1 advance payment at a time.",
                    "Queue Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);

                isAdvancePaymentMode = false;
                lblExpiryTitle.Text = "New Expiry Date";
                btnProcessPayment.IsEnabled = false;
                btnProcessPayment.Opacity = 0.5;
                RecalculateFinancialsAndDates();
                return;
            }

            // If they have exactly 1 plan (Active), ask if they want to extend it into the 1 available advance slot
            if (member.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) || existingCount == 1)
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
            if (!_validationHelper.ValidateAll())
            {
                return;
            }

            double paid = double.Parse(txtAmountPaid.Text);
            string paymentMode = rbCash.IsChecked == true ? "Cash" : "GCash";
            double change = paid - totalAmount;

            // 1. Keep this declaration exactly as it is here
            string formattedPlanDescription = durationMultiplier > 1
                ? $"{selectedMembershipType} ({durationMultiplier})"
                : selectedMembershipType;

            // 2. We removed the old paymentPrefix string completely here. 
            // The summary text now drops the prefix and uses the clean plan description.
            string summary = $"Please confirm the following payment:\n\n" +
                             $"  Member: {selectedMember!.FullName}\n" +
                             $"  Plan: {formattedPlanDescription}\n" + // Changed from paymentPrefix + formattedPlanDescription
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
                            cmd.Parameters.AddWithValue("@id", selectedMember!.MemberID);
                            cmd.Parameters.AddWithValue("@mname", selectedMember!.FullName);
                            cmd.Parameters.AddWithValue("@paid", paid);
                            cmd.Parameters.AddWithValue("@total", totalAmount);
                            cmd.Parameters.AddWithValue("@change", change);
                            cmd.Parameters.AddWithValue("@discountAmount", totalDiscountDeduction);
                            cmd.Parameters.AddWithValue("@mode", paymentMode);

                            // 3. Pass formattedPlanDescription directly here without any prefix variables
                            cmd.Parameters.AddWithValue("@type", formattedPlanDescription);

                            cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@expiry", lblNewExpiryDate.Text);
                            cmd.ExecuteNonQuery();
                        }

                        string memberSql = "UPDATE Members SET Status = 'Active', ExpiryDate = @expiry WHERE MemberID = @mid";
                        using (var cmd = new SQLiteCommand(memberSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@expiry", lblNewExpiryDate.Text);
                            cmd.Parameters.AddWithValue("@mid", selectedMember!.MemberID);
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

            lblOriginalSubtotal.Visibility = Visibility.Collapsed;
            lblDiscountDeductionDisplay.Visibility = Visibility.Collapsed;
            RefreshRateDiscountBadges();
            _validationHelper?.ClearErrors();
        }
    }
}
