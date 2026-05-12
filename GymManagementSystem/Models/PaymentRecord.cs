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
        public double AmountPaid { get; set; }
        public string PaymentMode { get; set; } = string.Empty;
        public string MembershipType { get; set; } = string.Empty;
        public string DateOfTransaction { get; set; } = string.Empty;
    }
}
