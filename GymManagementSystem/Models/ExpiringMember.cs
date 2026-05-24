using System;

namespace GymManagementSystem.Models
{
    public class ExpiringMember
    {
        public string FullName { get; set; } = "";
        public string MemberID { get; set; } = "";
        public string Phone { get; set; } = "";
        public string ExpiryDate { get; set; } = "";
        public int DaysRemaining { get; set; }
        public string DaysLeft { get; set; } = "";
        public string? LastNotifiedDate { get; set; }
    }
}
