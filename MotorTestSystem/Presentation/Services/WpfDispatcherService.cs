using System;
using System.Threading.Tasks;
using System.Windows;
using MotorTestSystem.Services;

namespace MotorTestSystem.Presentation.Services
{
    public class WpfDispatcherService : IDispatcherService
    {
        public void Invoke(Action action)
        {
            if (Application.Current == null || Application.Current.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(action);
            }
        }

        public Task InvokeAsync(Action action)
        {
            if (Application.Current == null || Application.Current.Dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }
            else
            {
                return Application.Current.Dispatcher.InvokeAsync(action).Task;
            }
        }

        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            if (Application.Current == null || Application.Current.Dispatcher.CheckAccess())
            {
                return Task.FromResult(func());
            }
            else
            {
                return Application.Current.Dispatcher.InvokeAsync(func).Task;
            }
        }
    }
}
