using System;

namespace GymManagementSystem.Models
{
    public class InstructorAttendanceRecord
    {
        public int AttendanceID { get; set; }
        public string InstructorID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string CheckInTime { get; set; } = string.Empty;
        public string CheckOutTime { get; set; } = string.Empty;
    }
}
