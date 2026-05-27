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
        public int DurationValue { get; set; } = 1;
        public string DurationUnit { get; set; } = "Days";
        public bool IsArchived { get; set; } = false;
        public double ApplicableDiscountPercentage { get; set; }
        
        public string ApplicableDiscountType { get; set; } = "Percentage";
        public double ApplicableDiscountValue { get; set; }
        
        public bool HasApplicableDiscount => ApplicableDiscountValue > 0 || ApplicableDiscountPercentage > 0;
        
        public string DiscountBadgeText
        {
            get
            {
                if (ApplicableDiscountType == "FixedAmount" && ApplicableDiscountValue > 0)
                    return $"₱{ApplicableDiscountValue:G0} OFF";
                
                double pct = ApplicableDiscountPercentage > 0 ? ApplicableDiscountPercentage : ApplicableDiscountValue;
                if (pct > 0)
                    return $"{pct:G0}% OFF";
                    
                return string.Empty;
            }
        }
    }
}