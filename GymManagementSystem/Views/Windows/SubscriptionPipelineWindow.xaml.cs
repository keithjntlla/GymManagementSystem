using GymManagementSystem.Models;
using GymManagementSystem.Views.MainViews;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace GymManagementSystem.Views.Windows
{
    public partial class SubscriptionPipelineWindow : Window
    {
        private Member _member;
        private MembersView _parentView;

        public SubscriptionPipelineWindow(Member member, MembersView parentView)
        {
            InitializeComponent();
            _member = member;
            _parentView = parentView;
            lblMemberDetails.Text = $"{_member.FullName} ({_member.MemberID})";
            LoadPipeline();
        }

        private void LoadPipeline()
        {
            var queuedPlans = new List<QueuedPlanItem>();

            // Set defaults up front
            lblActiveType.Text = "-";
            lblActiveStart.Text = "-";
            lblActiveExpiry.Text = "-";

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // 1. Fetch the exact payment row tracking what is active right now
                    string activeSql = @"
                SELECT MembershipType, DateOfTransaction, NewExpiryDate 
                FROM Payments 
                WHERE MemberID = @mid
                  AND Date(NewExpiryDate) >= Date('now')
                  AND IFNULL(PaymentMode, '') <> 'Refund'
                  AND IFNULL(PaymentMode, '') <> 'Refunded'
                  AND IFNULL(MembershipType, '') NOT LIKE '[REFUND]%'
                  AND IFNULL(MembershipType, '') NOT LIKE '[REFUNDED]%'
                ORDER BY PaymentID ASC LIMIT 1";

                    DateTime currentHorizon = DateTime.Today;

                    using (var cmd = new SQLiteCommand(activeSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@mid", _member.MemberID);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                lblActiveType.Text = reader["MembershipType"].ToString();
                                lblActiveStart.Text = reader["DateOfTransaction"].ToString();
                                lblActiveExpiry.Text = reader["NewExpiryDate"].ToString();

                                if (DateTime.TryParse(lblActiveExpiry.Text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime parsedHorizon))
                                {
                                    currentHorizon = parsedHorizon;
                                }
                            }
                        }
                    }

                    // 2. Fetch trailing advanced payment records
                    string queueSql = @"
                SELECT PaymentID, MembershipType, DateOfTransaction, NewExpiryDate, TotalAmount
                FROM Payments 
                WHERE MemberID = @mid
                  AND Date(NewExpiryDate) >= Date('now')
                  AND IFNULL(PaymentMode, '') <> 'Refund'
                  AND IFNULL(PaymentMode, '') <> 'Refunded'
                  AND IFNULL(MembershipType, '') NOT LIKE '[REFUND]%'
                  AND IFNULL(MembershipType, '') NOT LIKE '[REFUNDED]%'
                ORDER BY PaymentID ASC";

                    using (var cmd = new SQLiteCommand(queueSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@mid", _member.MemberID);
                        using (var reader = cmd.ExecuteReader())
                        {
                            int rowCounter = 0;
                            DateTime trackingStart = currentHorizon;

                            while (reader.Read())
                            {
                                rowCounter++;
                                if (rowCounter == 1) continue; // Skip the active plan line

                                // ── NEW FIX: STRICTLY CAP THE UI DISPLAY TO 1 QUEUED PLAN MAXIMUM ──
                                if (queuedPlans.Count >= 1)
                                {
                                    break; // Stop processing further entries if database has anomalies
                                }

                                int paymentId = Convert.ToInt32(reader["PaymentID"]);
                                string rawType = reader["MembershipType"].ToString() ?? "";
                                string planName = rawType;
                                string durationDesc = "1 session";

                                int multiplier = 1;
                                if (rawType.Contains("(") && rawType.Contains(")"))
                                {
                                    int startIdx = rawType.IndexOf("(");
                                    int endIdx = rawType.IndexOf(")");
                                    planName = rawType.Substring(0, startIdx).Trim();
                                    string multStr = rawType.Substring(startIdx + 1, endIdx - startIdx - 1);
                                    int.TryParse(multStr, out multiplier);
                                }

                                int daysPerPlan = planName.Equals("Daily", StringComparison.OrdinalIgnoreCase) ? 1 :
                                                  planName.Equals("Weekly", StringComparison.OrdinalIgnoreCase) ? 7 :
                                                  planName.Equals("Half-Month", StringComparison.OrdinalIgnoreCase) ? 15 :
                                                  planName.Equals("Monthly", StringComparison.OrdinalIgnoreCase) ? 30 : 365;

                                int totalDays = daysPerPlan * multiplier;
                                durationDesc = planName.Equals("Daily", StringComparison.OrdinalIgnoreCase) ? $"{totalDays} days" : rawType;

                                DateTime planStart = trackingStart.AddDays(1);
                                DateTime planEnd = trackingStart.AddDays(totalDays);

                                trackingStart = planEnd;

                                queuedPlans.Add(new QueuedPlanItem
                                {
                                    QueueNumber = queuedPlans.Count + 1,
                                    PaymentID = paymentId,
                                    PlanName = rawType,
                                    StartDate = planStart,
                                    ExpiryDate = planEnd,
                                    DurationDescription = durationDesc,
                                    RefundAmount = Convert.ToDouble(reader["TotalAmount"])
                                });
                            }
                        }
                    }
                }

                icQueuedPlans.ItemsSource = queuedPlans;
                lblQueueTitle.Text = $"Queued Plans ({queuedPlans.Count})";

                // ── NEW FIX: CHANGE CAPACITY LIMIT FROM 3 TO 1 ──
                if (queuedPlans.Count >= 1)
                {
                    btnAddAdvance.IsEnabled = false;
                    btnAddAdvance.Opacity = 0.5;
                    btnAddAdvance.ToolTip = "Maximum advanced queue capacity reached (Limit: 1)";
                }
                else
                {
                    btnAddAdvance.IsEnabled = true;
                    btnAddAdvance.Opacity = 1.0;
                    btnAddAdvance.ToolTip = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading subscription pipeline data: " + ex.Message);
            }
        }

        private void RefundPlan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is int paymentId)
            {
                var result = MessageBox.Show(
                    "Are you sure you want to refund this advanced plan payment?\n\nThis will remove the plan from the queue and log a negative financial reversal record.",
                    "Confirm Advanced Refund", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                try
                {
                    string refreshedExpiryDate = _member.ExpiryDate;
                    string refreshedStatus = _member.Status;

                    using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        using (var trans = conn.BeginTransaction())
                        {
                            // 1. Get transaction profile details for financial logs before clearing
                            double refundAmount = 0;
                            string memberId = _member.MemberID;
                            string memberName = _member.FullName;
                            string planType = "";
                            string processedBy = LoginWindow.CurrentUser?.Username ?? "System";

                            string checkSql = "SELECT TotalAmount, MembershipType FROM Payments WHERE PaymentID = @pid";
                            using (var cmd = new SQLiteCommand(checkSql, conn))
                            {
                                cmd.Parameters.AddWithValue("@pid", paymentId);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        refundAmount = Convert.ToDouble(reader["TotalAmount"]);
                                        planType = reader["MembershipType"].ToString() ?? "Plan";
                                    }
                                }
                            }

                            // 2. Keep the original queued payment for audit, but mark it so it no longer behaves as a queued plan.
                            string markRefundedSql = @"
                                UPDATE Payments
                                SET PaymentMode = 'Refunded',
                                    MembershipType = @refundedType
                                WHERE PaymentID = @pid";
                            using (var cmd = new SQLiteCommand(markRefundedSql, conn))
                            {
                                cmd.Parameters.AddWithValue("@refundedType", $"[REFUNDED] {planType}");
                                cmd.Parameters.AddWithValue("@pid", paymentId);
                                cmd.ExecuteNonQuery();
                            }

                            // 3. Re-calculate maximum baseline expiry profile parameter for core member record
                            string maxExpirySql = @"
                                SELECT MAX(Date(NewExpiryDate))
                                FROM Payments
                                WHERE MemberID = @mid
                                  AND IFNULL(PaymentMode, '') <> 'Refund'
                                  AND IFNULL(PaymentMode, '') <> 'Refunded'
                                  AND IFNULL(MembershipType, '') NOT LIKE '[REFUND]%'
                                  AND IFNULL(MembershipType, '') NOT LIKE '[REFUNDED]%'";
                            string newMaxExpiry = "-";
                            using (var cmd = new SQLiteCommand(maxExpirySql, conn))
                            {
                                cmd.Parameters.AddWithValue("@mid", memberId);
                                var maxObj = cmd.ExecuteScalar();
                                if (maxObj != DBNull.Value && maxObj != null)
                                {
                                    newMaxExpiry = Convert.ToDateTime(maxObj).ToString("yyyy-MM-dd");
                                }
                            }

                            refreshedExpiryDate = newMaxExpiry;
                            refreshedStatus = newMaxExpiry == "-" ? "Expired" : "Active";

                            string updateRefundedExpirySql = "UPDATE Payments SET NewExpiryDate = @expiry WHERE PaymentID = @pid";
                            using (var cmd = new SQLiteCommand(updateRefundedExpirySql, conn))
                            {
                                cmd.Parameters.AddWithValue("@expiry", newMaxExpiry);
                                cmd.Parameters.AddWithValue("@pid", paymentId);
                                cmd.ExecuteNonQuery();
                            }

                            string updateMemberSql = "UPDATE Members SET ExpiryDate = @expiry WHERE MemberID = @mid";
                            using (var cmd = new SQLiteCommand(updateMemberSql, conn))
                            {
                                cmd.Parameters.AddWithValue("@expiry", newMaxExpiry);
                                cmd.Parameters.AddWithValue("@mid", memberId);
                                cmd.ExecuteNonQuery();
                            }

                            // 4. Record the negative reversal audit logging event record entry line item
                            string refundLogSql = @"
                                INSERT INTO Payments (MemberID, MemberName, AmountPaid, TotalAmount, Change, PaymentMode, MembershipType, DateOfTransaction, NewExpiryDate, DiscountAmount, ProcessedBy) 
                                VALUES (@mid, @mname, 0, @amt, 0, 'Refund', @type, @date, @expiry, 0, @processedBy)";
                            using (var cmd = new SQLiteCommand(refundLogSql, conn))
                            {
                                cmd.Parameters.AddWithValue("@mid", memberId);
                                cmd.Parameters.AddWithValue("@mname", memberName);
                                cmd.Parameters.AddWithValue("@amt", -refundAmount);
                                cmd.Parameters.AddWithValue("@type", $"[REFUND] {planType}");
                                cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@expiry", newMaxExpiry);
                                cmd.Parameters.AddWithValue("@processedBy", processedBy);
                                cmd.ExecuteNonQuery();
                            }

                            trans.Commit();
                        }
                    }

                    _member.ExpiryDate = refreshedExpiryDate;
                    _member.Status = refreshedStatus;

                    MessageBox.Show("Advanced plan payment successfully refunded.", "Refund Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadPipeline();
                    _parentView.LoadMembers(); // Force refresh grid background collections components safely
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error processing queue refund event sequence: " + ex.Message);
                }
            }
        }

        private void AddAdvancePlan_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            var mainWindow = Window.GetWindow(_parentView) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.MainFrame.Content = new PaymentsView(_member);
                mainWindow.btnNavPayments.IsChecked = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
