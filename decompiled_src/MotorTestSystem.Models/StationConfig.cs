using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;

namespace MotorTestSystem.Models;

public class StationConfig : ObservableObject
{
	[ObservableProperty]
	private string _id = string.Empty;

	[ObservableProperty]
	private string _name = string.Empty;

	[ObservableProperty]
	private string _plcModel = string.Empty;

	[ObservableProperty]
	private string _ipAddress = "192.168.1.1";

	[ObservableProperty]
	private int _port = 502;

	[ObservableProperty]
	private string _protocol = "ModbusTCP";

	[ObservableProperty]
	private byte _stationId = 1;

	[ObservableProperty]
	private bool _isConnected = false;

	[ObservableProperty]
	private string _status = "离线";

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Id
	{
		get
		{
			return _id;
		}
		[MemberNotNull("_id")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_id, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Id);
				_id = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Id);
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
	public string PlcModel
	{
		get
		{
			return _plcModel;
		}
		[MemberNotNull("_plcModel")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_plcModel, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PlcModel);
				_plcModel = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PlcModel);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string IpAddress
	{
		get
		{
			return _ipAddress;
		}
		[MemberNotNull("_ipAddress")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_ipAddress, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IpAddress);
				_ipAddress = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IpAddress);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int Port
	{
		get
		{
			return _port;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_port, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Port);
				_port = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Port);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Protocol
	{
		get
		{
			return _protocol;
		}
		[MemberNotNull("_protocol")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_protocol, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Protocol);
				_protocol = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Protocol);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public byte StationId
	{
		get
		{
			return _stationId;
		}
		set
		{
			if (!EqualityComparer<byte>.Default.Equals(_stationId, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.StationId);
				_stationId = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.StationId);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsConnected
	{
		get
		{
			return _isConnected;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isConnected, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsConnected);
				_isConnected = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsConnected);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Status
	{
		get
		{
			return _status;
		}
		[MemberNotNull("_status")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_status, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Status);
				_status = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Status);
			}
		}
	}
}
