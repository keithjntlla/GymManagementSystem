using System;
using System.Collections.Generic;
using System.Text;

namespace GymManagementSystem.Models
{
    public class GymPlan
    {
        public int RateID { get; set; }
        public string PlanName { get; set; } = "";
        public double Price { get; set; }
        public int DurationDays { get; set; }
        public bool IsArchived { get; set; } = false;
        public double ApplicableDiscountPercentage { get; set; }
        public bool HasApplicableDiscount => ApplicableDiscountPercentage > 0;
        public string DiscountBadgeText => ApplicableDiscountPercentage > 0
            ? $"{ApplicableDiscountPercentage:G0}% OFF"
            : string.Empty;
    }
}