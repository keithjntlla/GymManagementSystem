using System;

namespace GymManagementSystem.Models
{
    public class QueuedPlanItem
    {
        public int QueueNumber { get; set; }
        public int PaymentID { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string DurationDescription { get; set; } = string.Empty;
        public double RefundAmount { get; set; }
    }
}
