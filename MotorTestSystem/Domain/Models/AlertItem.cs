using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MotorTestSystem.Models
{
    public partial class AlertItem : ObservableObject
    {
        public string Message { get; }
        public string Timestamp { get; }

        [ObservableProperty]
        private bool _isHandled;

        public IRelayCommand MarkHandledCommand { get; }

        public AlertItem(string message, Action onHandled)
        {
            Message = message;
            Timestamp = DateTime.Now.ToString("HH:mm:ss");
            MarkHandledCommand = new RelayCommand(
                () => { IsHandled = true; onHandled(); },
                () => !IsHandled);
        }

        partial void OnIsHandledChanged(bool _)
        {
            MarkHandledCommand.NotifyCanExecuteChanged();
        }
    }
}
