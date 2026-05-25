using System;

namespace GymManagementSystem.Models
{
    public class Instructor
    {
        public string InstructorID { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MiddleInitial { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // Computed display name
        public string FullName => string.IsNullOrWhiteSpace(MiddleInitial)
            ? $"{FirstName} {LastName}"
            : $"{FirstName} {MiddleInitial}. {LastName}";

        public string Phone { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public DateTime? Birthday { get; set; }
        public string Specialization { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string PhotoPath { get; set; } = string.Empty;
        public string DateHired { get; set; } = string.Empty; // Mapped to default Date Joined internally

        // Dynamic Client Assignments
        public int ClientCount { get; set; } = 0;
        public string ClientCountText => $"👤 {ClientCount} Clients";
    }
}
