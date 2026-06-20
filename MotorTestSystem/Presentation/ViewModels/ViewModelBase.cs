using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MotorTestSystem.ViewModels
{
    public abstract class ViewModelBase : ObservableObject, IDisposable
    {
        private bool _isDisposed;

        public bool IsDisposed => _isDisposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            _isDisposed = true;
        }
    }
}
