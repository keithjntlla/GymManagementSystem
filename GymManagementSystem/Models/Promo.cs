using System;

namespace GymManagementSystem.Models
{
    public class Promo
    {
        public int PromoID { get; set; }
        public string PromoCode { get; set; } = string.Empty;
        public string PromoName { get; set; } = string.Empty;
        public string DiscountType { get; set; } = "Percentage"; // "Percentage" or "FixedAmount"
        public double DiscountValue { get; set; }
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string ApplicableRates { get; set; } = "All";
        public bool IsActive { get; set; } = true;
        public bool IsArchived { get; set; } = false;

        // Custom helpers for display in DataGrid
        public string DiscountValueDisplay => DiscountType == "Percentage" 
            ? $"{DiscountValue:0.##}%" 
            : $"₱{DiscountValue:N2}";

        public string DateRangeDisplay => $"{StartDate} to {EndDate}";

        public string StatusDisplay
        {
            get
            {
                if (IsArchived) return "Archived";
                if (!IsActive) return "Inactive";
                
                if (DateTime.TryParse(StartDate, out DateTime start) && DateTime.Today < start.Date)
                    return "Upcoming";
                if (DateTime.TryParse(EndDate, out DateTime end) && DateTime.Today > end.Date)
                    return "Expired";
                    
                return "Active";
            }
        }

        public string StatusColor => StatusDisplay switch
        {
            "Active" => "#00a651",    // bright green
            "Upcoming" => "#ffab00",  // amber
            "Expired" => "#d50000",   // red
            "Archived" => "#757575",  // grey
            _ => "#757575"
        };
    }
}
