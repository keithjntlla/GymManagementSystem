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
    }
}
