using System;
using System.Text.RegularExpressions;

namespace GymManagementSystem
{
    public static class InputValidator
    {
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

            // Check if it contains only numbers
            if (!Regex.IsMatch(cleaned, @"^\d+$"))
            {
                return (false, "", "Phone number can only contain numeric digits.");
            }

            // Check length (10-11 digits for Philippine numbers)
            if (cleaned.Length < 10 || cleaned.Length > 11)
            {
                return (false, "", "Phone number must be between 10 and 11 digits.");
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
