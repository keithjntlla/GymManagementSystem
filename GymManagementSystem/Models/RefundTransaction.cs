using System;

namespace GymManagementSystem.Models
{
    public class RefundTransaction
    {
        public string RefundId { get; set; } = string.Empty;
        public string FormattedDate { get; set; } = string.Empty;
        public string FormattedTime { get; set; } = string.Empty;
        public string MemberId { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public double OriginalAmount { get; set; }
        public double RefundAmount { get; set; }
        public string ProcessedBy { get; set; } = string.Empty;
        public string RefundReason { get; set; } = string.Empty;
        public string RefundNotes { get; set; } = string.Empty;
    }
}
