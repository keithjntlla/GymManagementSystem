using System;

namespace GymManagementSystem
{
    public static class ExpirationUiHelper
    {
        public static int CalcDaysRemaining(string expiryDateStr)
        {
            if (DateTime.TryParse(expiryDateStr, out DateTime expiry))
                return (int)(expiry.Date - DateTime.Now.Date).TotalDays;
            return 0;
        }

        public static string ClassifyUrgency(int days)
        {
            if (days < 0) return "Expired";
            if (days <= 3) return "Critical";
            return "Warning";
        }

        public static string BuildDaysLabel(int days)
        {
            if (days < 0) return $"{Math.Abs(days)} days ago";
            if (days == 0) return "Expires Today";
            if (days == 1) return "1 Day Remaining";
            return $"{days} days";
        }

        public static string FormatExpiryDate(string raw)
        {
            if (DateTime.TryParse(raw, out DateTime dt))
                return dt.ToString("yyyy-MM-dd");
            return raw;
        }
    }
}
