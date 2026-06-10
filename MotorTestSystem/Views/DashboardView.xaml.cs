using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MotorTestSystem.ViewModels;

namespace MotorTestSystem.Views
{
    public partial class DashboardView : UserControl
    {
        // 2 * PI * 22 ≈ 138.23
        private const double Circumference = 138.23;

        public DashboardView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DashboardViewModel oldVm)
            {
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            }
            if (e.NewValue is DashboardViewModel newVm)
            {
                newVm.PropertyChanged += OnViewModelPropertyChanged;
                UpdatePassRateRing(newVm.PassRateRingDash, newVm.PassRateRingText);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DashboardViewModel.PassRateRingDash)
                or nameof(DashboardViewModel.PassRateRingText))
            {
                if (DataContext is DashboardViewModel vm)
                {
                    UpdatePassRateRing(vm.PassRateRingDash, vm.PassRateRingText);
                }
            }
        }

        private void UpdatePassRateRing(double progress, string text)
        {
            progress = Math.Clamp(progress, 0, 1);
            double filled = progress * Circumference;
            double gap = Circumference - filled;

            PassRateRingEllipse.StrokeDashArray = new DoubleCollection { filled, gap };
            PassRateRingText.Text = text;
        }
    }
}
