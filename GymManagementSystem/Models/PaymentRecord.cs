using System;
using System.Collections.Generic;
using System.Text;

namespace GymManagementSystem.Models
{
    public class PaymentRecord
    {
        public int PaymentID { get; set; }
        public string MemberID { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public string MemberType { get; set; } = string.Empty;
        public double AmountPaid { get; set; }
        public double TotalAmount { get; set; }
        public string PaymentMode { get; set; } = string.Empty;
        public string MembershipType { get; set; } = string.Empty;
        public string DateOfTransaction { get; set; } = string.Empty;
        public double DiscountAmount { get; set; }
        public string PromoCode { get; set; } = string.Empty;

        public bool HasDiscount => DiscountAmount > 0;
        public double SubtotalAmount => TotalAmount + DiscountAmount;

        public string DiscountDescription
        {
            get
            {
                if (DiscountAmount <= 0) return string.Empty;
                if (!string.IsNullOrEmpty(PromoCode))
                {
                    return $"Promo ({PromoCode})";
                }
                return $"{MemberType} Discount";
            }
        }

        public bool IsRefundTransaction =>
            PaymentMode.Equals("Refund", StringComparison.OrdinalIgnoreCase) ||
            MembershipType.StartsWith("[REFUND]", StringComparison.OrdinalIgnoreCase);

        public string DisplayMembershipType
        {
            get
            {
                const string refundPrefix = "[REFUND]";
                const string refundedPrefix = "[REFUNDED]";

                if (MembershipType.StartsWith(refundPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return MembershipType.Substring(refundPrefix.Length).Trim();
                }

                if (MembershipType.StartsWith(refundedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return MembershipType.Substring(refundedPrefix.Length).Trim();
                }

                return MembershipType;
            }
        }
    }
}
