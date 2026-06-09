using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace MotorTestSystem.Views;

public class KpiCardControl : UserControl, IComponentConnector
{
	public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(KpiCardControl), new PropertyMetadata((object)string.Empty));

	public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(string), typeof(KpiCardControl), new PropertyMetadata((object)string.Empty));

	public static readonly DependencyProperty ValueForegroundProperty = DependencyProperty.Register("ValueForeground", typeof(Brush), typeof(KpiCardControl), new PropertyMetadata((object)Brushes.White));

	private bool _contentLoaded;

	public string Title
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(TitleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TitleProperty, (object)value);
		}
	}

	public string Value
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(ValueProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ValueProperty, (object)value);
		}
	}

	public Brush ValueForeground
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(ValueForegroundProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ValueForegroundProperty, (object)value);
		}
	}

	public KpiCardControl()
	{
		InitializeComponent();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.5.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/MotorTestSystem;component/views/kpicardcontrol.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.5.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		_contentLoaded = true;
	}
}
