using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MotorTestSystem.Views
{
    public partial class KpiCardControl : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(KpiCardControl), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(string), typeof(KpiCardControl), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ValueForegroundProperty =
            DependencyProperty.Register(nameof(ValueForeground), typeof(Brush), typeof(KpiCardControl), new PropertyMetadata(Brushes.White));

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(string), typeof(KpiCardControl), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty TrendTextProperty =
            DependencyProperty.Register(nameof(TrendText), typeof(string), typeof(KpiCardControl), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty TrendForegroundProperty =
            DependencyProperty.Register(nameof(TrendForeground), typeof(Brush), typeof(KpiCardControl), new PropertyMetadata(Brushes.White));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public Brush ValueForeground
        {
            get => (Brush)GetValue(ValueForegroundProperty);
            set => SetValue(ValueForegroundProperty, value);
        }

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public string TrendText
        {
            get => (string)GetValue(TrendTextProperty);
            set => SetValue(TrendTextProperty, value);
        }

        public Brush TrendForeground
        {
            get => (Brush)GetValue(TrendForegroundProperty);
            set => SetValue(TrendForegroundProperty, value);
        }

        public KpiCardControl()
        {
            InitializeComponent();
        }
    }
}
