using System;
using System.ComponentModel;

namespace GymManagementSystem.Models
{
    public class RateSelectionWrapper : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string PlanName { get; set; } = string.Empty;
        public bool IsSelected
        {
            get => _isSelected;
            set 
            { 
                _isSelected = value; 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); 
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
