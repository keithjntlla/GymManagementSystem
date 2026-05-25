using System;
using System.Data.SQLite;
using System.Windows;
using GymManagementSystem.Models;
using GymManagementSystem.Views.MainViews;
using GymManagementSystem.Views.Windows;

namespace GymManagementSystem
{
    public static class PaymentNavigationHelper
    {
        public static bool TryNavigateToPayment(DependencyObject context, string memberId)
        {
            DatabaseHelper.RefreshMemberStatuses();
            if (string.IsNullOrWhiteSpace(memberId))
                return false;

            int existingCount = 0;
            Member? member = null;

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
                        cmd.Parameters.AddWithValue("@mid", memberId);
                        existingCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    }

                    string memberSql = "SELECT * FROM Members WHERE MemberID = @mid LIMIT 1";
                    using (var cmd = new SQLiteCommand(memberSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@mid", memberId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                                return false;

                            string dbStatus = reader["Status"]?.ToString() ?? "Pending";
                            string expiryStr = reader["ExpiryDate"]?.ToString() ?? string.Empty;

                            if (!string.IsNullOrWhiteSpace(expiryStr) && DateTime.TryParse(expiryStr, out DateTime expiryDate))
                            {
                                expiryStr = expiryDate.ToString("yyyy-MM-dd");
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
                                expiryStr = "-";
                                if (dbStatus.Equals("Active", StringComparison.OrdinalIgnoreCase))
                                {
                                    dbStatus = "Expired";
                                }
                            }

                            member = new Member
                            {
                                MemberID = reader["MemberID"]?.ToString() ?? string.Empty,
                                FirstName = reader["FirstName"]?.ToString() ?? string.Empty,
                                MiddleInitial = reader["MiddleInitial"]?.ToString() ?? string.Empty,
                                LastName = reader["LastName"]?.ToString() ?? string.Empty,
                                Phone = reader["Phone"]?.ToString() ?? string.Empty,
                                Gender = reader["Gender"]?.ToString() ?? string.Empty,
                                MemberType = reader["MemberType"]?.ToString() ?? "Regular",
                                Status = dbStatus,
                                PhotoPath = reader["PhotoPath"]?.ToString() ?? string.Empty,
                                ExpiryDate = expiryStr,
                                DateJoined = reader["DateJoined"]?.ToString() ?? string.Empty
                            };

                            if (reader["Birthday"] != DBNull.Value
                                && DateTime.TryParse(reader["Birthday"].ToString(), out DateTime birthday))
                            {
                                member.Birthday = birthday;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading member for payment: " + ex.Message,
                    "Payment", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (existingCount >= 2)
            {
                MessageBox.Show(
                    "Action Denied: This member already has an advanced plan waiting in the pipeline.\n\n" +
                    "You cannot add more than 1 advance payment at a time.",
                    "Queue Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (member == null)
                return false;

            bool? advancePaymentConfirmed = null;
            bool needsAdvancePrompt = member.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)
                                      || existingCount == 1;

            if (needsAdvancePrompt)
            {
                var result = MessageBox.Show(
                    $"Member {member.FullName} is still Active (Expires: {member.ExpiryDate}).\n\n" +
                    "Would you like to process an Advance Payment? This will extend their current plan without losing remaining days.",
                    "Active Membership Detected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return false;

                advancePaymentConfirmed = true;
            }
            else
            {
                advancePaymentConfirmed = false;
            }

            var mainWindow = Window.GetWindow(context) as MainWindow;
            if (mainWindow == null)
                return false;

            mainWindow.MainFrame.Content = new PaymentsView(member, advancePaymentConfirmed);
            mainWindow.btnNavPayments.IsChecked = true;
            return true;
        }
    }
}
