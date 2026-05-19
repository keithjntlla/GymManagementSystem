using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace GymManagementSystem.Models
{
    public class Member
    {
        // Core Identifiers
        public string MemberID { get; set; } = string.Empty;

        // Split Name Fields (Matches txtFirstName, txtMiddleInitial, txtLastName)
        public string FirstName { get; set; } = string.Empty;
        public string MiddleInitial { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // Automatically computes FullName for backward compatibility or DataGrids
        public string FullName => string.IsNullOrWhiteSpace(MiddleInitial)
            ? $"{FirstName} {LastName}"
            : $"{FirstName} {MiddleInitial}. {LastName}";

        // Contact & Demographics
        public string Phone { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public DateTime? Birthday { get; set; } // New field (Nullable to prevent default min-value errors)

        // Membership Details
        public MembershipType MemberType { get; set; } = MembershipType.Regular;
        public string DateJoined { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty;
        public string MembershipPlan { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        // Media
        public string PhotoPath { get; set; } = string.Empty;
    }
}