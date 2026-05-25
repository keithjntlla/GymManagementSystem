using System.Windows;
using System.Windows.Controls;

namespace GymManagementSystem.Views.Windows
{
    /// <summary>
    /// Modal dialog that requires the user to select a refund reason before
    /// a refund can be processed. Returns SelectedReason and Notes on confirmation.
    /// </summary>
    public partial class RefundReasonWindow : Window
    {
        /// <summary>The selected preset reason. Null if cancelled.</summary>
        public string? SelectedReason { get; private set; }

        /// <summary>Optional free-text notes entered by the user.</summary>
        public string Notes { get; private set; } = string.Empty;

        private readonly string _planName;
        private readonly double _refundAmount;

        public RefundReasonWindow(string planName, double refundAmount)
        {
            InitializeComponent();
            _planName = planName;
            _refundAmount = refundAmount;
            lblSubtitle.Text = $"Plan: {planName}  ·  Amount: ₱{refundAmount:N2}";
        }

        private void CbReason_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Guard: event fires during InitializeComponent before btnConfirm exists
            if (btnConfirm == null) return;

            // Enable the Confirm button only when a real reason is chosen (index > 0)
            bool hasValidReason = cbReason.SelectedIndex > 0;
            btnConfirm.IsEnabled = hasValidReason;
            btnConfirm.Opacity = hasValidReason ? 1.0 : 0.5;
            lblReasonError.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (cbReason.SelectedIndex <= 0)
            {
                lblReasonError.Visibility = System.Windows.Visibility.Visible;
                return;
            }

            SelectedReason = (cbReason.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
            Notes = txtNotes.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedReason = null;
            DialogResult = false;
            Close();
        }
    }
}
