using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

namespace GymManagementSystem
{
    public static class InputValidator
    {
        /// <summary>
        /// Validates a First or Last Name.
        /// Required, max 50 characters, only alphabetic, spaces, hyphens, apostrophes.
        /// Trims whitespaces.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateName(string input, string fieldName = "Name")
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", $"{fieldName} cannot be empty.");
            }

            string cleaned = input.Trim();
            // Remove multiple spaces
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            if (cleaned.Length > 50)
            {
                return (false, "", $"{fieldName} cannot exceed 50 characters.");
            }

            // Allowed: alphabetic characters, spaces, hyphens, apostrophes
            if (!Regex.IsMatch(cleaned, @"^[a-zA-Z\s'-]+$"))
            {
                return (false, "", $"{fieldName} can only contain letters, spaces, hyphens, and apostrophes.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates Middle Initial.
        /// Optional, exactly 1 character, alphabetic, auto-uppercased.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateMiddleInitial(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (true, "", ""); // Optional
            }

            string cleaned = input.Trim().ToUpper();

            if (cleaned.Length != 1)
            {
                return (false, "", "Middle Initial must be exactly 1 character.");
            }

            if (!Regex.IsMatch(cleaned, @"^[A-Z]$"))
            {
                return (false, "", "Middle Initial must be a letter.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates strict Philippines Mobile format: 09XXXXXXXXX (starts with 09, exactly 11 digits).
        /// Allow typing anything, validated on blur (LostFocus).
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidatePhoneNumber(string input, string fieldName = "Phone number")
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", $"{fieldName} cannot be empty.");
            }

            string cleaned = input.Trim();

            // Must contain only numeric digits
            if (!Regex.IsMatch(cleaned, @"^\d+$"))
            {
                return (false, "", $"{fieldName} must contain numeric digits only. Spaces, letters, hyphens, or symbols are not allowed.");
            }

            if (cleaned.Length != 11)
            {
                return (false, "", $"{fieldName} must be exactly 11 digits.");
            }

            if (!cleaned.StartsWith("09"))
            {
                return (false, "", $"{fieldName} must start precisely with '09'.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates Birthday.
        /// Format strictly DD/MM/YYYY.
        /// Reject impossible dates.
        /// Age must be strictly between 14 and 100.
        /// Birthday must strictly be in the past (cannot be today or future).
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateBirthday(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Birthday is required.");
            }

            string cleaned = input.Trim();
            string format = "dd/MM/yyyy";

            if (!DateTime.TryParseExact(cleaned, format, 
                                        CultureInfo.InvariantCulture, 
                                        DateTimeStyles.None, 
                                        out DateTime parsedDate))
            {
                return (false, "", "Birthday must strictly follow the DD/MM/YYYY format.");
            }

            if (parsedDate >= DateTime.Today)
            {
                return (false, "", "Birthday must be a date in the past (cannot be today or in the future).");
            }

            // Age calculation
            int age = DateTime.Today.Year - parsedDate.Year;
            if (parsedDate > DateTime.Today.AddYears(-age))
            {
                age--;
            }

            if (age < 14)
            {
                return (false, "", "Member must be at least 14 years old.");
            }

            if (age > 100)
            {
                return (false, "", "Age cannot exceed 100 years.");
            }

            // Return standardized string representation (yyyy-MM-dd) for SQLite
            return (true, parsedDate.ToString("dd/MM/yyyy"), "");
        }

        /// <summary>
        /// Validates uploaded image format and size constraints.
        /// Allowed extensions: .jpg, .jpeg, .png.
        /// Maximum file size: 5MB.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidatePhoto(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return (true, "", ""); // Photo is optional
            }

            if (!File.Exists(filePath))
            {
                return (false, "", "Selected photo file does not exist.");
            }

            string extension = Path.GetExtension(filePath).ToLower();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
            {
                return (false, "", "Only .jpg, .jpeg, and .png image formats are allowed.");
            }

            try
            {
                long fileLengthBytes = new FileInfo(filePath).Length;
                double sizeInMb = fileLengthBytes / (1024.0 * 1024.0);

                if (sizeInMb > 5.0)
                {
                    return (false, "", "Selected image is too large. Maximum file size allowed is 5MB.");
                }
            }
            catch (Exception ex)
            {
                return (false, "", $"Error reading file properties: {ex.Message}");
            }

            return (true, filePath, "");
        }

        /// <summary>
        /// Validates Gym Name field.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateGymName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Gym Name cannot be empty.");
            }

            string cleaned = input.Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

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
        /// Validates Address field.
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

            if (cleaned.Length > 200)
            {
                return (false, "", "Address cannot exceed 200 characters.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates Email Address.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateEmail(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Email address cannot be empty.");
            }

            string cleaned = input.Trim().ToLower(); // Convert to lowercase for uniform DB storage

            // Strict pattern: ends only with .com, .ph, .net, or .org (case-insensitive)
            string emailPattern = @"^[^@\s]+@[^@\s]+\.(com|ph|net|org)$";

            if (!Regex.IsMatch(cleaned, emailPattern))
            {
                return (false, "", "Please enter a valid email address ending in .com, .ph, .net, or .org.");
            }

            if (cleaned.Length > 100)
            {
                return (false, "", "Email address cannot exceed 100 characters.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates Gender selection.
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
        /// Validates Member Type selection.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateMemberType(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Member Type Tier must be selected.");
            }

            return (true, input.Trim(), "");
        }

        /// <summary>
        /// Validates User Accounts Username.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateUsername(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Username cannot be empty.");
            }

            string cleaned = input.Trim();
            if (cleaned.Length < 3)
            {
                return (false, "", "Username must be at least 3 characters long.");
            }

            if (cleaned.Length > 50)
            {
                return (false, "", "Username cannot exceed 50 characters.");
            }

            if (!Regex.IsMatch(cleaned, @"^[a-zA-Z0-9_]+$"))
            {
                return (false, "", "Username can only contain alphanumeric characters and underscores.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates User Accounts/Change Password.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidatePassword(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return (false, "", "Password cannot be empty.");
            }

            if (input.Length < 6)
            {
                return (false, "", "Password must be at least 6 characters long.");
            }

            if (input.Length > 64)
            {
                return (false, "", "Password cannot exceed 64 characters.");
            }

            return (true, input, "");
        }

        /// <summary>
        /// Validates Discount Tier Name.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateDiscountName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Discount tier name cannot be empty.");
            }

            string cleaned = input.Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            if (cleaned.Length > 50)
            {
                return (false, "", "Discount tier name cannot exceed 50 characters.");
            }

            if (!Regex.IsMatch(cleaned, @"^[a-zA-Z\s]+$"))
            {
                return (false, "", "Discount tier name can only contain alphabetic characters and spaces.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates Discount Percentage.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateDiscountPercentage(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Discount percentage is required.");
            }

            string cleaned = input.Trim();

            if (!double.TryParse(cleaned, out double val))
            {
                return (false, "", "Discount percentage must be a valid number.");
            }

            if (val < 0 || val > 100)
            {
                return (false, "", "Discount percentage must be between 0 and 100.");
            }

            return (true, val.ToString("0.##"), "");
        }

        /// <summary>
        /// Validates Gym Plan Name.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidatePlanName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Plan name cannot be empty.");
            }

            string cleaned = input.Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            if (cleaned.Length > 50)
            {
                return (false, "", "Plan name cannot exceed 50 characters.");
            }

            if (!Regex.IsMatch(cleaned, @"^[a-zA-Z0-9\s-]+$"))
            {
                return (false, "", "Plan name can only contain alphanumeric characters, spaces, and hyphens.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Validates Plan Price.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidatePlanPrice(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Price is required.");
            }

            string cleaned = input.Trim();

            if (!double.TryParse(cleaned, out double price))
            {
                return (false, "", "Price must be a valid number.");
            }

            if (price < 0)
            {
                return (false, "", "Price cannot be a negative value.");
            }

            return (true, price.ToString("0.##"), "");
        }

        /// <summary>
        /// Validates Plan Duration.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidatePlanDuration(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Duration is required.");
            }

            string cleaned = input.Trim();

            if (!int.TryParse(cleaned, out int days))
            {
                return (false, "", "Duration must be an integer number of days.");
            }

            if (days <= 0)
            {
                return (false, "", "Duration must be at least 1 day.");
            }

            return (true, days.ToString(), "");
        }

        /// <summary>
        /// Validates Amount Paid.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateAmountPaid(string input, double totalAmount)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Amount paid cannot be empty.");
            }

            string cleaned = input.Trim();

            if (!double.TryParse(cleaned, out double paid))
            {
                return (false, "", "Amount paid must be a valid number.");
            }

            if (paid < 0)
            {
                return (false, "", "Amount paid cannot be negative.");
            }

            if (paid < totalAmount)
            {
                return (false, "", $"Insufficient payment. Must pay at least ₱{totalAmount:N2}.");
            }

            return (true, paid.ToString("0.##"), "");
        }

        /// <summary>
        /// Validates Instructor Birthday.
        /// Format strictly DD/MM/YYYY.
        /// Age must be strictly between 14 and 100.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateInstructorBirthday(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Birthday is required.");
            }

            string cleaned = input.Trim();
            string format = "dd/MM/yyyy";

            if (!DateTime.TryParseExact(cleaned, format, 
                                        CultureInfo.InvariantCulture, 
                                        DateTimeStyles.None, 
                                        out DateTime parsedDate))
            {
                return (false, "", "Birthday must strictly follow the DD/MM/YYYY format.");
            }

            if (parsedDate >= DateTime.Today)
            {
                return (false, "", "Birthday must be a date in the past (cannot be today or in the future).");
            }

            // Age calculation
            int age = DateTime.Today.Year - parsedDate.Year;
            if (parsedDate > DateTime.Today.AddYears(-age))
            {
                age--;
            }

            if (age < 14)
            {
                return (false, "", "Instructor must be at least 14 years old.");
            }

            if (age > 100)
            {
                return (false, "", "Age cannot exceed 100 years.");
            }

            return (true, parsedDate.ToString("dd/MM/yyyy"), "");
        }

        /// <summary>
        /// Validates Instructor Specialization field.
        /// </summary>
        public static (bool isValid, string cleanedValue, string errorMessage) ValidateSpecialization(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "", "Specialization cannot be empty.");
            }

            string cleaned = input.Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            if (cleaned.Length > 200)
            {
                return (false, "", "Specialization cannot exceed 200 characters.");
            }

            return (true, cleaned, "");
        }

        /// <summary>
        /// Hashes a password string using SHA-256 algorithm.
        /// </summary>
        public static string HashPassword(string password)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                var sb = new System.Text.StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}