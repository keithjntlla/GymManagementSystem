using System;
using System.Collections.Generic;
using System.Text;

namespace GymManagementSystem.Models
{
    public class User
    {
        public string UserID { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = ""; // Stored as Hashed string
        public string Role { get; set; } = "";     // "Administrator" or "Staff"
        public string CreatedDate { get; set; } = ""; 
        public string Status { get; set; } = "Active"; // "Active" or "Inactive"
    }
}
