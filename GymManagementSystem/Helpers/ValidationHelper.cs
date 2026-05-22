using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GymManagementSystem
{
    public class ValidationHelper
    {
        private class FieldBinding
        {
            public Control Control { get; set; } = null!;
            public TextBlock ErrorLabel { get; set; } = null!;
            public Func<(bool isValid, string cleanedValue, string errorMessage)> Validator { get; set; } = null!;
            public Action<string>? ValueSetter { get; set; }
            public Brush DefaultBorderBrush { get; set; } = null!;
            public bool HasLostFocusWired { get; set; }
        }

        private readonly List<FieldBinding> _fields = new List<FieldBinding>();
        private readonly Brush _errorBorderBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68)); // Premium red #FF4444
        private readonly Brush _defaultBorderBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51)); // Default dark gray #333333

        /// <summary>
        /// Registers a generic control for validation.
        /// </summary>
        public void RegisterField(
            Control control, 
            TextBlock errorLabel, 
            Func<(bool isValid, string cleanedValue, string errorMessage)> validator,
            Action<string>? valueSetter = null)
        {
            if (control == null || errorLabel == null || validator == null) return;

            // Preserve current border brush as the default to restore later
            Brush originalBorder = control.BorderBrush ?? _defaultBorderBrush;

            var binding = new FieldBinding
            {
                Control = control,
                ErrorLabel = errorLabel,
                Validator = validator,
                ValueSetter = valueSetter,
                DefaultBorderBrush = originalBorder,
                HasLostFocusWired = false
            };

            _fields.Add(binding);

            // Wire up LostFocus for real-time validation
            control.LostFocus += (s, e) =>
            {
                ValidateBinding(binding);
            };
            binding.HasLostFocusWired = true;

            // Initialize error state as collapsed
            errorLabel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Registers a standard TextBox for validation.
        /// </summary>
        public void RegisterTextBox(
            TextBox textBox, 
            TextBlock errorLabel, 
            Func<string, (bool isValid, string cleanedValue, string errorMessage)> validator)
        {
            RegisterField(
                textBox, 
                errorLabel, 
                () => validator(textBox.Text), 
                cleaned => { textBox.Text = cleaned; }
            );
        }

        /// <summary>
        /// Registers a DatePicker for validation.
        /// </summary>
        public void RegisterDatePicker(
            DatePicker datePicker, 
            TextBlock errorLabel, 
            Func<string, (bool isValid, string cleanedValue, string errorMessage)> validator)
        {
            RegisterField(
                datePicker, 
                errorLabel, 
                () => validator(datePicker.Text), 
                cleaned => { 
                    // Set both selected date and text to align picker and text box
                    if (DateTime.TryParseExact(cleaned, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime dt))
                    {
                        datePicker.SelectedDate = dt;
                    }
                }
            );
        }

        /// <summary>
        /// Registers a ComboBox for validation.
        /// </summary>
        public void RegisterComboBox(
            ComboBox comboBox, 
            TextBlock errorLabel, 
            Func<string, (bool isValid, string cleanedValue, string errorMessage)> validator)
        {
            RegisterField(
                comboBox, 
                errorLabel, 
                () => validator(comboBox.Text), 
                null
            );
        }

        /// <summary>
        /// Registers a PasswordBox for validation.
        /// </summary>
        public void RegisterPasswordBox(
            PasswordBox passwordBox, 
            TextBlock errorLabel, 
            Func<string, (bool isValid, string cleanedValue, string errorMessage)> validator)
        {
            RegisterField(
                passwordBox, 
                errorLabel, 
                () => validator(passwordBox.Password), 
                null
            );
        }

        /// <summary>
        /// Validates a single binding field, updating its visual state.
        /// </summary>
        private bool ValidateBinding(FieldBinding binding)
        {
            var (isValid, cleanedValue, errorMessage) = binding.Validator();

            if (isValid)
            {
                // Reset visuals to healthy state
                binding.Control.BorderBrush = binding.DefaultBorderBrush;
                binding.ErrorLabel.Text = string.Empty;
                binding.ErrorLabel.Visibility = Visibility.Collapsed;

                // Update control content with cleaned, sanitized values if setter is defined
                if (binding.ValueSetter != null && cleanedValue != null)
                {
                    binding.ValueSetter(cleanedValue);
                }

                return true;
            }
            else
            {
                // Highlight invalid state
                binding.Control.BorderBrush = _errorBorderBrush;
                binding.ErrorLabel.Text = errorMessage;
                binding.ErrorLabel.Visibility = Visibility.Visible;
                return false;
            }
        }

        /// <summary>
        /// Validates all registered fields in order.
        /// Focuses the first invalid control and returns false.
        /// Returns true if all fields are valid.
        /// </summary>
        public bool ValidateAll()
        {
            FieldBinding? firstInvalid = null;
            bool isAllValid = true;

            foreach (var binding in _fields)
            {
                bool isFieldValid = ValidateBinding(binding);
                if (!isFieldValid)
                {
                    isAllValid = false;
                    if (firstInvalid == null)
                    {
                        firstInvalid = binding;
                    }
                }
            }

            if (firstInvalid != null)
            {
                // Move focus to the first invalid field
                firstInvalid.Control.Focus();
            }

            return isAllValid;
        }

        /// <summary>
        /// Clears all validation errors and restores default brushes.
        /// </summary>
        public void ClearErrors()
        {
            foreach (var binding in _fields)
            {
                binding.Control.BorderBrush = binding.DefaultBorderBrush;
                binding.ErrorLabel.Text = string.Empty;
                binding.ErrorLabel.Visibility = Visibility.Collapsed;
            }
        }
    }
}
