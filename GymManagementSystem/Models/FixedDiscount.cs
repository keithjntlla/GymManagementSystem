using System;
using System.Collections.Generic;
using System.Text;

namespace GymManagementSystem.Models
{
    public class FixedDiscount
    {
        public string DiscountID { get; set; } = Guid.NewGuid().ToString();
        public string TargetType { get; set; } = string.Empty;
        public double Percentage { get; set; }
        public string ApplicableRates { get; set; } = "All";
        public bool IsArchived { get; set; } 
    }
}
