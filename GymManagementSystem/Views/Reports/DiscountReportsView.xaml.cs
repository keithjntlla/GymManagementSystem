using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace GymManagementSystem.Views.Reports
{
    public partial class DiscountReportsView : UserControl
    {
        public ObservableCollection<PromoAuditItem> PromoCampaigns { get; set; }
            = new ObservableCollection<PromoAuditItem>();

        public ObservableCollection<TierAuditItem> TierSavings { get; set; }
            = new ObservableCollection<TierAuditItem>();

        public DiscountReportsView()
        {
            InitializeComponent();
            dgPromoAudit.ItemsSource = PromoCampaigns;
            dgTierAudit.ItemsSource = TierSavings;
            LoadDiscountAuditData();
        }

        private void LoadDiscountAuditData()
        {
            PromoCampaigns.Clear();
            TierSavings.Clear();

            int activeCampaigns = 0;
            double totalPromoSavings = 0;
            int totalPromoRedemptions = 0;
            double totalTierSavings = 0;
            int totalTierRedemptions = 0;

            try
            {
                using (var conn = new SQLiteConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // 1. Fetch Active Promo Campaigns Count
                    string activePromosSql = @"
                        SELECT COUNT(*) FROM Promos 
                        WHERE IsActive = 1 
                          AND IsArchived = 0 
                          AND Date('now') BETWEEN Date(StartDate) AND Date(EndDate)";
                    using (var cmd = new SQLiteCommand(activePromosSql, conn))
                    {
                        activeCampaigns = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    }

                    // 2. Fetch Promo Campaigns Audit List
                    string promosQuerySql = @"
                        SELECT P.PromoCode, P.PromoName, P.DiscountType, P.DiscountValue, P.StartDate, P.EndDate, P.IsActive, P.IsArchived,
                               COUNT(MP.PromoCode) AS RedemptionCount,
                               COALESCE(SUM(PAY.DiscountAmount), 0) AS TotalDiscountGiven
                        FROM Promos P
                        LEFT JOIN MemberPromos MP ON UPPER(P.PromoCode) = UPPER(MP.PromoCode)
                        LEFT JOIN Payments PAY ON MP.PaymentID = PAY.PaymentID
                        WHERE P.IsArchived = 0
                        GROUP BY P.PromoCode
                        ORDER BY RedemptionCount DESC, P.PromoCode ASC";

                    using (var cmd = new SQLiteCommand(promosQuerySql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string code = reader["PromoCode"]?.ToString() ?? "";
                            string name = reader["PromoName"]?.ToString() ?? "";
                            string discType = reader["DiscountType"]?.ToString() ?? "Percentage";
                            double discVal = Convert.ToDouble(reader["DiscountValue"]);
                            string startStr = reader["StartDate"]?.ToString() ?? "";
                            string endStr = reader["EndDate"]?.ToString() ?? "";
                            int isActive = reader["IsActive"] != DBNull.Value ? Convert.ToInt32(reader["IsActive"]) : 1;
                            int redemptionCount = Convert.ToInt32(reader["RedemptionCount"]);
                            double totalSavingsGiven = Convert.ToDouble(reader["TotalDiscountGiven"]);

                            // Calculate Status
                            string status = "Active";
                            if (isActive == 0)
                            {
                                status = "Inactive";
                            }
                            else if (DateTime.TryParse(startStr, out DateTime start) && DateTime.Today < start.Date)
                            {
                                status = "Inactive";
                            }
                            else if (DateTime.TryParse(endStr, out DateTime end) && DateTime.Today > end.Date)
                            {
                                status = "Expired";
                            }

                            string displayVal = discType == "FixedAmount" ? $"₱{discVal:N2} off" : $"{discVal:0.##}% off";

                            var promoItem = new PromoAuditItem
                            {
                                PromoCode = code,
                                CampaignName = name,
                                DisplayValue = displayVal,
                                RedemptionCount = redemptionCount,
                                TotalSavings = totalSavingsGiven,
                                CampaignStatus = status
                            };

                            PromoCampaigns.Add(promoItem);
                            totalPromoSavings += totalSavingsGiven;
                            totalPromoRedemptions += redemptionCount;
                        }
                    }

                    // 3. Fetch Fixed Tier Audits List (Student, Senior)
                    string tierQuerySql = @"
                        SELECT D.TargetType, D.Percentage,
                               COUNT(PAY.PaymentID) AS RedemptionCount,
                               COALESCE(SUM(PAY.DiscountAmount), 0) AS TotalSavingsGiven
                        FROM Discounts D
                        LEFT JOIN (
                            SELECT P.PaymentID, P.DiscountAmount, M.MemberType
                            FROM Payments P
                            INNER JOIN Members M ON P.MemberID = M.MemberID
                            WHERE P.PaymentID NOT IN (SELECT DISTINCT PaymentID FROM MemberPromos WHERE PaymentID IS NOT NULL)
                              AND P.DiscountAmount > 0
                        ) PAY ON LOWER(PAY.MemberType) = LOWER(D.TargetType)
                        GROUP BY D.TargetType, D.Percentage
                        ORDER BY D.TargetType ASC";

                    using (var cmd = new SQLiteCommand(tierQuerySql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string targetType = reader["TargetType"]?.ToString() ?? "";
                            double pct = Convert.ToDouble(reader["Percentage"]);
                            int redemptionCount = Convert.ToInt32(reader["RedemptionCount"]);
                            double totalSavingsGiven = Convert.ToDouble(reader["TotalSavingsGiven"]);

                            var tierItem = new TierAuditItem
                            {
                                TierName = $"{targetType} Tier",
                                DisplayValue = $"{pct:0.##}% Discount",
                                RedemptionCount = redemptionCount,
                                TotalSavings = totalSavingsGiven
                            };

                            TierSavings.Add(tierItem);
                            totalTierSavings += totalSavingsGiven;
                            totalTierRedemptions += redemptionCount;
                        }
                    }
                }

                // Update UI Summary Cards
                lblActiveCampaignsCount.Text = activeCampaigns.ToString();
                lblPromoSavings.Text = $"₱{totalPromoSavings:N2}";
                lblPromoSavingsCount.Text = $"{totalPromoRedemptions} redemptions";
                lblTierSavings.Text = $"₱{totalTierSavings:N2}";
                lblTierSavingsCount.Text = $"{totalTierRedemptions} transactions";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading discount audit dashboard: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class PromoAuditItem
    {
        public string PromoCode { get; set; } = string.Empty;
        public string CampaignName { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = string.Empty;
        public int RedemptionCount { get; set; }
        public double TotalSavings { get; set; }
        public string CampaignStatus { get; set; } = string.Empty;
    }

    public class TierAuditItem
    {
        public string TierName { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = string.Empty;
        public int RedemptionCount { get; set; }
        public double TotalSavings { get; set; }
    }
}
