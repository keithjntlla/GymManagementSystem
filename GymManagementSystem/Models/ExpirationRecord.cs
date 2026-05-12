using System;
using System.Collections.Generic;
using System.Text;

namespace GymManagementSystem.Models
{
    public class ExpirationRecord
    {
        public string MemberID { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string PlanType { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty;
        public int DaysRemaining { get; set; }
        public string DaysRemainingLabel { get; set; } = string.Empty;
        public string UrgencyLevel { get; set; } = string.Empty;
        public double ProgressWidth { get; set; }
    }
}
