using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GymManagementSystem.Models
{
    public class NotificationMemberItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        private bool _canNotify = true;

        public string MemberID { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty;
        public int DaysRemaining { get; set; }
        public string DaysRemainingLabel { get; set; } = string.Empty;
        public string UrgencyLevel { get; set; } = string.Empty;
        public string? LastNotifiedDate { get; set; }

        public string NotifiedStatusLabel { get; set; } = string.Empty;

        public bool CanNotify
        {
            get => _canNotify;
            set
            {
                if (_canNotify == value) return;
                _canNotify = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                if (!CanNotify && value)
                    return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
