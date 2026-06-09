using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;

namespace MotorTestSystem.ViewModels;

public class UserEditDialogViewModel : ViewModelBase
{
	[ObservableProperty]
	private string _title = "新增用户";

	[ObservableProperty]
	private string _account = string.Empty;

	[ObservableProperty]
	private string _name = string.Empty;

	[ObservableProperty]
	private string _selectedRole = "操作员";

	[ObservableProperty]
	private bool _isEnabled = true;

	[ObservableProperty]
	private bool _isDisabled = false;

	public ObservableCollection<string> Roles { get; } = new ObservableCollection<string> { "管理员", "操作员", "维护员" };

	public string Password { get; set; } = string.Empty;

	public string ConfirmPassword { get; set; } = string.Empty;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Title
	{
		get
		{
			return _title;
		}
		[MemberNotNull("_title")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_title, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Title);
				_title = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Title);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Account
	{
		get
		{
			return _account;
		}
		[MemberNotNull("_account")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_account, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Account);
				_account = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Account);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Name
	{
		get
		{
			return _name;
		}
		[MemberNotNull("_name")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_name, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Name);
				_name = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Name);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string SelectedRole
	{
		get
		{
			return _selectedRole;
		}
		[MemberNotNull("_selectedRole")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_selectedRole, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedRole);
				_selectedRole = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedRole);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsEnabled
	{
		get
		{
			return _isEnabled;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isEnabled, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsEnabled);
				_isEnabled = value;
				OnIsEnabledChanged(value);
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsEnabled);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsDisabled
	{
		get
		{
			return _isDisabled;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isDisabled, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsDisabled);
				_isDisabled = value;
				OnIsDisabledChanged(value);
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsDisabled);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsEnabledChanged(bool value)
	{
		if (value)
		{
			_isDisabled = false;
			((ObservableObject)this).OnPropertyChanged("IsDisabled");
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsDisabledChanged(bool value)
	{
		if (value)
		{
			_isEnabled = false;
			((ObservableObject)this).OnPropertyChanged("IsEnabled");
		}
	}
}
