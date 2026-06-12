using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 海康威视SDK封装服务
    /// 提供摄像头登录、预览、抓图等功能
    /// </summary>
    public class HikvisionSdkService : IDisposable
    {
        #region Native SDK 常量定义

        private const int NET_DVR_DEV_ADDRESS_MAX_LEN = 128;
        private const int NET_DVR_LOGIN_USERNAME_MAX_LEN = 64;
        private const int NET_DVR_LOGIN_PASSWD_MAX_LEN = 64;
        private const int SERIALNO_LEN = 48;

        // 登录结果常量
        private const int NET_DVR_SUCCESS = 0;
        private const int NET_DVR_PASSWORD_ERROR = 1;
        private const int NET_DVR_USER_NO_EXIST = 2;
        private const int NET_DVR_LOGIN_TIMEOUT = 3;
        private const int NET_DVR_NET_RECV_CONNECT_ERROR = 8;

        // 预览参数
        private const int NET_DVR_REALDATA = 1;  // 实时流
        private const int NET_DVR_PLAYRTSP = 4;   // RTSP流

        // 抓图参数
        private const int NET_DVR_JPEG = 0;
        private const int NET_DVR_PICTURE_QUALITY = 0;

        #endregion

        #region Native SDK 结构体定义

        [StructLayout(LayoutKind.Sequential)]
        public struct NET_DVR_DEVICEINFO_V30
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = SERIALNO_LEN)]
            public byte[] sSerialNumber;
            public byte byAlarmInPortNum;
            public byte byAlarmOutPortNum;
            public byte byDiskNum;
            public byte byDVRType;
            public byte byChanNum;
            public byte byStartChan;
            public byte byAudioChanNum;
            public byte byIPChanNum;
            public byte byZeroChanNum;
            public byte byMainProto;
            public byte bySubProto;
            public byte bySupport;
            public byte bySupport1;
            public byte bySupport2;
            public ushort wDevType;
            public byte bySupport3;
            public byte byMultiStreamProto;
            public byte byStartDChan;
            public byte byStartDTalkChan;
            public byte byHighDChanNum;
            public byte bySupport4;
            public byte byLanguageType;
            public byte byVoiceInChanNum;
            public byte byStartVoiceInChanNo;
            public byte bySupport5;
            public byte bySupport6;
            public byte byMirrorChanNum;
            public ushort wStartMirrorChanNo;
            public byte bySupport7;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NET_DVR_PREVIEWINFO
        {
            public int lChannel;
            public uint dwStreamType;
            public uint dwLinkMode;
            public int hPlayWnd;
            public uint dwDisplayBufNum;
            public byte byProtoType;
            public byte byPreviewMode;
            public byte byRes1;
            public byte byRes2;
        }

        #endregion

        #region Native SDK 委托定义

        public delegate void REALDATACALLBACK(int lRealHandle, uint dwDataType, IntPtr pBuffer, uint dwBufSize, IntPtr pUser);
        public delegate void DRAWFUN(int lRealHandle, IntPtr hDc, uint dwUser);

        #endregion

        #region Native SDK 函数导入

        [DllImport("HCNetSDK.dll")]
        private static extern bool NET_DVR_Init();

        [DllImport("HCNetSDK.dll")]
        private static extern bool NET_DVR_Cleanup();

        [DllImport("HCNetSDK.dll")]
        private static extern uint NET_DVR_GetLastError();

        [DllImport("HCNetSDK.dll")]
        private static extern int NET_DVR_Login_V30(string sDeviceAddress, int wPort, string sUserName, string sPassword, ref NET_DVR_DEVICEINFO_V30 lpDeviceInfo);

        [DllImport("HCNetSDK.dll")]
        private static extern bool NET_DVR_Logout(int iUserID);

        [DllImport("HCNetSDK.dll")]
        private static extern int NET_DVR_RealPlay_V40(int iUserID, ref NET_DVR_PREVIEWINFO lpPreviewInfo, REALDATACALLBACK fRealDataCallBack, IntPtr pUser);

        [DllImport("HCNetSDK.dll")]
        private static extern bool NET_DVR_StopRealPlay(int iRealHandle);

        [DllImport("HCNetSDK.dll")]
        private static extern bool NET_DVR_CapturePicture(int lRealHandle, string sPicFileName);

        [DllImport("HCNetSDK.dll")]
        private static extern bool NET_DVR_CapturePictureNew(int lRealHandle, ref NET_DVR_JPEGPARA pJpegPara, ref int pJpegSize, IntPtr pJpegBuffer);

        [DllImport("HCNetSDK.dll")]
        private static extern int NET_DVR_GetRealPlayerIndex(int iRealHandle);

        [DllImport("HCNetSDK.dll")]
        private static extern bool NET_DVR_SetConnectTime(uint dwWaitTime, uint dwTryTimes);

        #endregion

        #region 结构体定义

        [StructLayout(LayoutKind.Sequential)]
        public struct NET_DVR_JPEGPARA
        {
            public ushort wPicSize;
            public ushort wPicQuality;
        }

        #endregion

        #region 私有字段

        private bool _isInitialized = false;
        private readonly Dictionary<string, int> _loggedUsers = new();  // IP -> UserID
        private readonly Dictionary<int, int> _previewHandles = new();   // Channel -> PreviewHandle

        #endregion

        #region 公共属性

        /// <summary>
        /// SDK是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 最后错误代码
        /// </summary>
        public uint LastErrorCode => NET_DVR_GetLastError();

        #endregion

        #region 构造函数

        public HikvisionSdkService()
        {
            Initialize();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化SDK
        /// </summary>
        public bool Initialize()
        {
            if (_isInitialized)
                return true;

            try
            {
                _isInitialized = NET_DVR_Init();
                if (_isInitialized)
                {
                    // 设置连接超时时间和重试次数
                    NET_DVR_SetConnectTime(2000, 1);  // 2秒超时，重试1次
                }
                return _isInitialized;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"海康SDK初始化失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 登录设备
        /// </summary>
        /// <param name="ip">设备IP地址</param>
        /// <param name="port">端口号（默认8000）</param>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>登录结果</returns>
        public async Task<LoginResult> LoginAsync(string ip, int port, string username, string password)
        {
            return await Task.Run(() =>
            {
                if (!_isInitialized)
                {
                    return new LoginResult { Success = false, ErrorMessage = "SDK未初始化" };
                }

                // 如果已登录，先登出
                if (_loggedUsers.ContainsKey(ip))
                {
                    Logout(ip);
                }

                var deviceInfo = new NET_DVR_DEVICEINFO_V30();
                deviceInfo.sSerialNumber = new byte[SERIALNO_LEN];

                int userId = NET_DVR_Login_V30(ip, port, username, password, ref deviceInfo);

                if (userId < 0)
                {
                    uint errorCode = NET_DVR_GetLastError();
                    string errorMsg = GetLoginErrorMessage(errorCode);
                    return new LoginResult { Success = false, ErrorCode = (int)errorCode, ErrorMessage = errorMsg };
                }

                _loggedUsers[ip] = userId;

                return new LoginResult
                {
                    Success = true,
                    UserId = userId,
                    DeviceInfo = new DeviceInfo
                    {
                        SerialNumber = System.Text.Encoding.Default.GetString(deviceInfo.sSerialNumber).TrimEnd('\0'),
                        ChannelCount = deviceInfo.byChanNum,
                        StartChannel = deviceInfo.byStartChan,
                        DeviceType = deviceInfo.byDVRType
                    }
                };
            });
        }

        /// <summary>
        /// 登出设备
        /// </summary>
        public bool Logout(string ip)
        {
            if (_loggedUsers.TryGetValue(ip, out int userId))
            {
                bool result = NET_DVR_Logout(userId);
                _loggedUsers.Remove(ip);
                return result;
            }
            return true;
        }

        /// <summary>
        /// 开始实时预览
        /// </summary>
        /// <param name="ip">设备IP</param>
        /// <param name="channel">通道号</param>
        /// <param name="windowHandle">显示窗口句柄</param>
        /// <returns>预览句柄</returns>
        public int StartPreview(string ip, int channel, IntPtr windowHandle)
        {
            if (!_loggedUsers.TryGetValue(ip, out int userId))
            {
                return -1;
            }

            var previewInfo = new NET_DVR_PREVIEWINFO
            {
                lChannel = channel,
                dwStreamType = 0,  // 主码流
                dwLinkMode = 0,    // TCP方式
                hPlayWnd = windowHandle.ToInt32(),
                dwDisplayBufNum = 1,
                byProtoType = 0,   // 私有协议
                byPreviewMode = 0  // 正常预览
            };

            int realHandle = NET_DVR_RealPlay_V40(userId, ref previewInfo, null!, IntPtr.Zero);

            if (realHandle >= 0)
            {
                _previewHandles[channel] = realHandle;
            }

            return realHandle;
        }

        /// <summary>
        /// 停止实时预览
        /// </summary>
        public bool StopPreview(int channel)
        {
            if (_previewHandles.TryGetValue(channel, out int realHandle))
            {
                bool result = NET_DVR_StopRealPlay(realHandle);
                _previewHandles.Remove(channel);
                return result;
            }
            return true;
        }

        /// <summary>
        /// 抓图保存到文件
        /// </summary>
        public bool CapturePicture(int channel, string filePath)
        {
            if (_previewHandles.TryGetValue(channel, out int realHandle))
            {
                return NET_DVR_CapturePicture(realHandle, filePath);
            }
            return false;
        }

        /// <summary>
        /// 获取所有已登录设备
        /// </summary>
        public IReadOnlyDictionary<string, int> GetLoggedDevices()
        {
            return _loggedUsers;
        }

        #endregion

        #region 私有方法

        private string GetLoginErrorMessage(uint errorCode)
        {
            return errorCode switch
            {
                NET_DVR_PASSWORD_ERROR => "密码错误",
                NET_DVR_USER_NO_EXIST => "用户不存在",
                NET_DVR_LOGIN_TIMEOUT => "登录超时",
                NET_DVR_NET_RECV_CONNECT_ERROR => "网络连接失败",
                _ => $"错误代码: {errorCode}"
            };
        }

        #endregion

        #region IDisposable 实现

        public void Dispose()
        {
            // 登出所有设备
            foreach (var kvp in _loggedUsers)
            {
                NET_DVR_Logout(kvp.Value);
            }
            _loggedUsers.Clear();

            // 停止所有预览
            foreach (var kvp in _previewHandles)
            {
                NET_DVR_StopRealPlay(kvp.Value);
            }
            _previewHandles.Clear();

            // 清理SDK
            if (_isInitialized)
            {
                NET_DVR_Cleanup();
                _isInitialized = false;
            }
        }

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// 登录结果
    /// </summary>
    public class LoginResult
    {
        public bool Success { get; set; }
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int UserId { get; set; }
        public DeviceInfo? DeviceInfo { get; set; }
    }

    /// <summary>
    /// 设备信息
    /// </summary>
    public class DeviceInfo
    {
        public string SerialNumber { get; set; } = string.Empty;
        public byte ChannelCount { get; set; }
        public byte StartChannel { get; set; }
        public byte DeviceType { get; set; }
    }

    #endregion
}
