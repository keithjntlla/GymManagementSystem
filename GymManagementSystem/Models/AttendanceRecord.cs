using System;

namespace GymManagementSystem
{
    public class AttendanceRecord
    {
        public int AttendanceID { get; set; }
        public string MemberID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string MembershipType { get; set; } = string.Empty;
        public string CheckInTime { get; set; } = string.Empty;
        public string CheckOutTime { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
