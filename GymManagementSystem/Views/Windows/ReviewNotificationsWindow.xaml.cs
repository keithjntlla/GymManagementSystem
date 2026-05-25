using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using GymManagementSystem.Models;

namespace GymManagementSystem.Views.Windows
{
    public partial class ReviewNotificationsWindow : Window
    {
        private readonly ObservableCollection<NotificationMemberItem> _members = new();

        public int NotifiedCount { get; private set; }

        public ReviewNotificationsWindow(IEnumerable<NotificationMemberItem> members)
        {
            InitializeComponent();

            foreach (var member in members)
            {
                if (NotificationHelper.WasNotifiedOnDate(member.LastNotifiedDate, NotificationHelper.Today))
                {
                    member.CanNotify = false;
                    member.IsSelected = false;
                    if (string.IsNullOrEmpty(member.NotifiedStatusLabel))
                        member.NotifiedStatusLabel = "(Already Notified Today)";
                }

                _members.Add(member);
            }

            itemsMembers.ItemsSource = _members;
            foreach (var member in _members)
                member.PropertyChanged += Member_PropertyChanged;

            UpdateSendButtonText();
        }

        private void Member_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NotificationMemberItem.IsSelected))
                UpdateSendButtonText();
        }

        private void UpdateSendButtonText()
        {
            int count = _members.Count(m => m.IsSelected && m.CanNotify);
            btnSend.Content = count == 1
                ? "Send (1) Notification"
                : $"Send ({count}) Notifications";
            btnSend.IsEnabled = count > 0;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            var selected = _members.Where(m => m.IsSelected && m.CanNotify).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Select at least one member to notify.",
                    "Review Notifications", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (selected.Any(m => NotificationHelper.WasNotifiedOnDate(m.LastNotifiedDate, NotificationHelper.Today)))
            {
                MessageBox.Show(
                    "One or more selected members have already been notified today. You can notify them again tomorrow.",
                    "Already Notified",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            btnSend.IsEnabled = false;
            btnSend.Content = "Sending...";

            var memberIds = selected.Select(m => m.MemberID).ToList();
            NotificationHelper.RecordNotifications(memberIds);

            NotifiedCount = memberIds.Count;
            DialogResult = true;
            Close();
        }
    }
}
