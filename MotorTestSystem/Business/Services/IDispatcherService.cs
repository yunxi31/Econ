using System;
using System.Threading.Tasks;

namespace MotorTestSystem.Services
{
    public interface IDispatcherService
    {
        void Invoke(Action action);
        Task InvokeAsync(Action action);
        Task<T> InvokeAsync<T>(Func<T> func);
    }
}
