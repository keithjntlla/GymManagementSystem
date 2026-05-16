using System;
using System.Text.RegularExpressions;

namespace GymManagementSystem
{
    public static class InputValidator
    {
        /// <summary>
        /// Validates and cleans the Gym Name field.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateGymName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Gym Name cannot be empty.");
            }

            string cleaned = input.Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " "); // Remove extra spaces

            if (cleaned.Length < 2)
            {
                return (false, "", "Gym Name must be at least 2 characters long.");
            }

            if (cleaned.Length > 100)
            {
                return (false, "", "Gym Name cannot exceed 100 characters.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates and cleans the Address field.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateAddress(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Address cannot be empty.");
            }

            string cleaned = input.Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            if (cleaned.Length < 5)
            {
                return (false, "", "Address must be at least 5 characters long.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates the Email Address field.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateEmail(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Email address cannot be empty.");
            }

            string cleaned = input.Trim();

            // Standard email regex pattern
            string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

            if (!Regex.IsMatch(cleaned, emailPattern))
            {
                return (false, "", "Please enter a valid email address (e.g., info@gym.com).");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates and cleans the Name field.
        /// Accepts only alphabetic characters and spaces.
        /// Automatically trims leading/trailing whitespace.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Name cannot be empty.");
            }

            // Trim leading and trailing whitespace
            string cleaned = input.Trim();

            // Remove extra spaces between words
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            // Check if it contains only alphabetic characters and spaces
            if (!Regex.IsMatch(cleaned, @"^[a-zA-Z\s]+$"))
            {
                return (false, "", "Name can only contain alphabetic characters and spaces. Numbers and special characters are not allowed.");
            }

            // Check minimum length
            if (cleaned.Length < 2)
            {
                return (false, "", "Name must be at least 2 characters long.");
            }

            // Check maximum length
            if (cleaned.Length > 100)
            {
                return (false, "", "Name cannot exceed 100 characters.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates the Phone Number field.
        /// Accepts only numeric characters.
        /// Optionally validates length (10-11 digits for Philippine numbers).
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidatePhoneNumber(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Phone number cannot be empty.");
            }

            // Remove all non-numeric characters
            string cleaned = Regex.Replace(input.Trim(), @"[^0-9]", "");

            if (cleaned.Length != 11)
            {
                return (false, "", "Phone number must be exactly 11 digits.");
            }

            // Must start with "09"
            if (!cleaned.StartsWith("09"))
            {
                return (false, "", "Phone number must start with '09'.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates the Gender field.
        /// Accepts: Male, Female, Other
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateGender(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Gender must be selected.");
            }

            string cleaned = input.Trim();
            if (cleaned != "Male" && cleaned != "Female" && cleaned != "Other")
            {
                return (false, "", "Gender must be Male, Female, or Other.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates the Date Joined field.
        /// Ensures the date is not in the future.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateDateJoined(DateTime? dateJoined)
        {
            if (!dateJoined.HasValue)
            {
                return (false, "", "Date Joined must be selected.");
            }

            if (dateJoined.Value > DateTime.Now)
            {
                return (false, "", "Date Joined cannot be in the future.");
            }

            return (true, dateJoined.Value.ToString("yyyy-MM-dd"), "");
        }
    }
}