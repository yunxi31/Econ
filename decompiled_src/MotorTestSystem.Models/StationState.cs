using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;

namespace MotorTestSystem.Models;

public class StationState : ObservableObject
{
	[ObservableProperty]
	private string _id = string.Empty;

	[ObservableProperty]
	private string _name = string.Empty;

	[ObservableProperty]
	private string _plcModel = string.Empty;

	[ObservableProperty]
	private string _protocol = string.Empty;

	[ObservableProperty]
	private string _barcode = "等待扫码...";

	[ObservableProperty]
	private int _status = 0;

	[ObservableProperty]
	private bool _isOnline = true;

	[ObservableProperty]
	private string _result = "WAIT";

	[ObservableProperty]
	private bool _handshakeSignal = false;

	[ObservableProperty]
	private double _noLoadCurrent;

	[ObservableProperty]
	private int _noLoadSpeed;

	[ObservableProperty]
	private double _shaftLength;

	[ObservableProperty]
	private double _knurlDiameter;

	[ObservableProperty]
	private double _fwdNoise;

	[ObservableProperty]
	private double _revNoise;

	[ObservableProperty]
	private double _noiseDiff;

	[ObservableProperty]
	private double _loadCurrent;

	[ObservableProperty]
	private int _loadSpeed;

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
	public string Barcode
	{
		get
		{
			return _barcode;
		}
		[MemberNotNull("_barcode")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_barcode, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Barcode);
				_barcode = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Barcode);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int Status
	{
		get
		{
			return _status;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_status, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Status);
				_status = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Status);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsOnline
	{
		get
		{
			return _isOnline;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isOnline, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsOnline);
				_isOnline = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsOnline);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Result
	{
		get
		{
			return _result;
		}
		[MemberNotNull("_result")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_result, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Result);
				_result = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Result);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HandshakeSignal
	{
		get
		{
			return _handshakeSignal;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_handshakeSignal, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HandshakeSignal);
				_handshakeSignal = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HandshakeSignal);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double NoLoadCurrent
	{
		get
		{
			return _noLoadCurrent;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_noLoadCurrent, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.NoLoadCurrent);
				_noLoadCurrent = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.NoLoadCurrent);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int NoLoadSpeed
	{
		get
		{
			return _noLoadSpeed;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_noLoadSpeed, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.NoLoadSpeed);
				_noLoadSpeed = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.NoLoadSpeed);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double ShaftLength
	{
		get
		{
			return _shaftLength;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_shaftLength, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ShaftLength);
				_shaftLength = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ShaftLength);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double KnurlDiameter
	{
		get
		{
			return _knurlDiameter;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_knurlDiameter, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.KnurlDiameter);
				_knurlDiameter = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.KnurlDiameter);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double FwdNoise
	{
		get
		{
			return _fwdNoise;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_fwdNoise, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FwdNoise);
				_fwdNoise = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FwdNoise);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double RevNoise
	{
		get
		{
			return _revNoise;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_revNoise, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.RevNoise);
				_revNoise = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RevNoise);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double NoiseDiff
	{
		get
		{
			return _noiseDiff;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_noiseDiff, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.NoiseDiff);
				_noiseDiff = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.NoiseDiff);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double LoadCurrent
	{
		get
		{
			return _loadCurrent;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_loadCurrent, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LoadCurrent);
				_loadCurrent = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LoadCurrent);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int LoadSpeed
	{
		get
		{
			return _loadSpeed;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_loadSpeed, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LoadSpeed);
				_loadSpeed = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LoadSpeed);
			}
		}
	}
}
