using System;
using System.Collections.Generic;
using System.Text;

namespace GymManagementSystem.Models
{
    public class Member
    {
        public string MemberID { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string DateJoined { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty;
        public string MembershipPlan { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string PhotoPath { get; set; } = string.Empty;
    }
}
