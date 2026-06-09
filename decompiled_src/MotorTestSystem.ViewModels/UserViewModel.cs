using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;

namespace MotorTestSystem.ViewModels;

public class UserViewModel : ViewModelBase
{
	public class UserItem : ObservableObject
	{
		private string _account = string.Empty;

		private string _name = string.Empty;

		private string _role = string.Empty;

		private string _status = string.Empty;

		private string _lastLoginTime = string.Empty;

		public string Account
		{
			get
			{
				return _account;
			}
			set
			{
				((ObservableObject)this).SetProperty<string>(ref _account, value, "Account");
			}
		}

		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				((ObservableObject)this).SetProperty<string>(ref _name, value, "Name");
			}
		}

		public string Role
		{
			get
			{
				return _role;
			}
			set
			{
				((ObservableObject)this).SetProperty<string>(ref _role, value, "Role");
			}
		}

		public string Status
		{
			get
			{
				return _status;
			}
			set
			{
				((ObservableObject)this).SetProperty<string>(ref _status, value, "Status");
			}
		}

		public string LastLoginTime
		{
			get
			{
				return _lastLoginTime;
			}
			set
			{
				((ObservableObject)this).SetProperty<string>(ref _lastLoginTime, value, "LastLoginTime");
			}
		}
	}

	private ObservableCollection<UserItem> _allUsers = new ObservableCollection<UserItem>();

	[ObservableProperty]
	private ObservableCollection<UserItem> _users = new ObservableCollection<UserItem>();

	[ObservableProperty]
	private string _searchText = string.Empty;

	[ObservableProperty]
	private string _selectedRoleFilter = "所有角色";

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? addUserCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<UserItem>? editUserCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<UserItem>? resetPasswordCommand;

	public ObservableCollection<string> RoleFilters { get; } = new ObservableCollection<string> { "所有角色", "管理员", "操作员", "维护员" };

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ObservableCollection<UserItem> Users
	{
		get
		{
			return _users;
		}
		[MemberNotNull("_users")]
		set
		{
			if (!EqualityComparer<ObservableCollection<UserItem>>.Default.Equals(_users, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Users);
				_users = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Users);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string SearchText
	{
		get
		{
			return _searchText;
		}
		[MemberNotNull("_searchText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_searchText, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SearchText);
				_searchText = value;
				OnSearchTextChanged(value);
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SearchText);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string SelectedRoleFilter
	{
		get
		{
			return _selectedRoleFilter;
		}
		[MemberNotNull("_selectedRoleFilter")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_selectedRoleFilter, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedRoleFilter);
				_selectedRoleFilter = value;
				OnSelectedRoleFilterChanged(value);
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedRoleFilter);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand AddUserCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected O, but got Unknown
			//IL_0023: Expected O, but got Unknown
			RelayCommand obj = addUserCommand;
			if (obj == null)
			{
				RelayCommand val = new RelayCommand((Action)AddUser);
				RelayCommand val2 = val;
				addUserCommand = val;
				obj = val2;
			}
			return (IRelayCommand)(object)obj;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<UserItem> EditUserCommand => (IRelayCommand<UserItem>)(object)(editUserCommand ?? (editUserCommand = new RelayCommand<UserItem>((Action<UserItem>)EditUser)));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<UserItem> ResetPasswordCommand => (IRelayCommand<UserItem>)(object)(resetPasswordCommand ?? (resetPasswordCommand = new RelayCommand<UserItem>((Action<UserItem>)ResetPassword)));

	public UserViewModel()
	{
		LoadMockUsers();
		FilterUsers();
	}

	private void LoadMockUsers()
	{
		_allUsers = new ObservableCollection<UserItem>
		{
			new UserItem
			{
				Account = "OP-10024",
				Name = "张伟 (Zhang Wei)",
				Role = "管理员",
				Status = "在线",
				LastLoginTime = "2023-10-27 08:15:32"
			},
			new UserItem
			{
				Account = "OP-10088",
				Name = "李娜 (Li Na)",
				Role = "操作员",
				Status = "在线",
				LastLoginTime = "2023-10-27 14:22:10"
			},
			new UserItem
			{
				Account = "MT-20011",
				Name = "王强 (Wang Qiang)",
				Role = "维护员",
				Status = "离线",
				LastLoginTime = "2023-10-26 18:45:00"
			},
			new UserItem
			{
				Account = "OP-10092",
				Name = "赵雷 (Zhao Lei)",
				Role = "操作员",
				Status = "禁用",
				LastLoginTime = "2023-10-15 09:12:44"
			},
			new UserItem
			{
				Account = "OP-10095",
				Name = "陈静 (Chen Jing)",
				Role = "操作员",
				Status = "在线",
				LastLoginTime = "2023-10-27 15:10:22"
			},
			new UserItem
			{
				Account = "MT-20015",
				Name = "刘洋 (Liu Yang)",
				Role = "维护员",
				Status = "离线",
				LastLoginTime = "2023-10-26 09:30:15"
			},
			new UserItem
			{
				Account = "OP-10102",
				Name = "周梅 (Zhou Mei)",
				Role = "操作员",
				Status = "在线",
				LastLoginTime = "2023-10-27 16:45:00"
			}
		};
	}

	private void FilterUsers()
	{
		IEnumerable<UserItem> enumerable = _allUsers.AsEnumerable();
		if (!string.IsNullOrWhiteSpace(SearchText))
		{
			enumerable = enumerable.Where((UserItem u) => u.Account.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || u.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
		}
		if (SelectedRoleFilter != "所有角色")
		{
			enumerable = enumerable.Where((UserItem u) => u.Role == SelectedRoleFilter);
		}
		Users = new ObservableCollection<UserItem>(enumerable);
	}

	[RelayCommand]
	private void AddUser()
	{
		UserEditDialogViewModel userEditDialogViewModel = new UserEditDialogViewModel
		{
			Title = "新增用户",
			IsEnabled = true
		};
		UserEditWindow userEditWindow = new UserEditWindow
		{
			DataContext = userEditDialogViewModel,
			Owner = Application.Current.MainWindow
		};
		if (userEditWindow.ShowDialog() == true)
		{
			UserItem item = new UserItem
			{
				Account = userEditDialogViewModel.Account,
				Name = userEditDialogViewModel.Name,
				Role = userEditDialogViewModel.SelectedRole,
				Status = (userEditDialogViewModel.IsEnabled ? "在线" : "禁用"),
				LastLoginTime = "-"
			};
			_allUsers.Insert(0, item);
			FilterUsers();
		}
	}

	[RelayCommand]
	private void EditUser(UserItem user)
	{
		if (user != null)
		{
			UserEditDialogViewModel userEditDialogViewModel = new UserEditDialogViewModel
			{
				Title = "编辑用户信息",
				Account = user.Account,
				Name = user.Name,
				SelectedRole = user.Role,
				IsEnabled = (user.Status != "禁用")
			};
			UserEditWindow userEditWindow = new UserEditWindow
			{
				DataContext = userEditDialogViewModel,
				Owner = Application.Current.MainWindow
			};
			if (userEditWindow.ShowDialog() == true)
			{
				user.Account = userEditDialogViewModel.Account;
				user.Name = userEditDialogViewModel.Name;
				user.Role = userEditDialogViewModel.SelectedRole;
				user.Status = (userEditDialogViewModel.IsEnabled ? "在线" : "禁用");
				FilterUsers();
			}
		}
	}

	[RelayCommand]
	private void ResetPassword(UserItem user)
	{
		if (user != null)
		{
			MessageBox.Show($"已重置用户 {user.Name} ({user.Account}) 的密码为初始密码。", "密码重置", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSearchTextChanged(string value)
	{
		FilterUsers();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedRoleFilterChanged(string value)
	{
		FilterUsers();
	}
}
