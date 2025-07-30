using DenseLight.Services;
using MvCameraControl;
using OpenCvSharp;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows;

namespace DenseLight.Devices
{
    public class HikCameraService : ICameraService, IDisposable
    {
        private readonly ILoggerService _logger;

        private bool _isInitialized = false;
        private bool _isGrabbing = false;
        private bool _disposed = false;

        public event EventHandler<Bitmap> FrameCaptured;

        IFrameOut frameOut = null;

        HikCamImplement hikCam = new();

        public IDevice? device;

        public HikCameraService(ILoggerService logger)
        {
            _logger = logger;
            InitializeSDK();
            Init();
            Open();
        }

        private void InitializeSDK()
        {
            SDKSystem.Initialize();
            _isInitialized = true;
            _logger.LogInformation("Hikvision SDK initialized successfully");
        }

        public bool Capture(out Mat mat)
        {
            if (!_isInitialized)
            {
                _logger.LogError("Hikvision SDK is not initialized.");
                mat = null;
                return false;
            }
            hikCam.device.StreamGrabber.GetImageBuffer(1000, out frameOut);

            // 替换原有的 mat = new Mat((int)frameOut.Image.Height, (int)frameOut.Image.Width, MatType.CV_8UC3, frameOut.Image.PixelData);
            // 使用 Mat.FromPixelData 静态方法来创建 Mat 实例
            mat = Mat.FromPixelData(
                (int)frameOut.Image.Height,
                (int)frameOut.Image.Width,
                MatType.CV_8UC3,
                frameOut.Image.PixelData
            );

            return true;
        }

        public string GetExposure() => hikCam.GetExposure();

        public bool GetExposureMax(ref double exposureMax)
        {
            return false;
        }

        public bool GetExposureMin(ref double exposureMin)
        {
            return false;
        }

        public string GetFrameRate() => hikCam.GetFrameRate();


        public string GetGain() => hikCam.GetGain();


        public bool Init() => hikCam.InitializeCam();


        public void SaveCapture(string path) => hikCam.SaveCapture(path);

        public bool SetExposure(float exposure) => hikCam.SetExposure(exposure);

        public bool SetGain(float value) => hikCam.SetGain(value);

        public void StartCapture() => hikCam.StartCapture();

        public void StopCapture() => hikCam.StopCapture();

        public void Dispose()
        {
            hikCam.Dispose();
            SDKSystem.Finalize();
        }

        public void Configure() => hikCam.ConfigureCam();

        public void Open() => hikCam.OpenCam();

        public string GetPixelFormat() => hikCam.GetPixelFormat();

        public bool SetAcquisitionFrameRate(float value) => hikCam.SetAcquisitionFrameRate(value);

        public void Close() => hikCam.CloseCam();

        ~HikCameraService()
        {
            Dispose();
        }
    }

    class HikCamImplement
    {
        private readonly DeviceTLayerType enumTLayerType = DeviceTLayerType.MvGigEDevice |
                DeviceTLayerType.MvUsbDevice | DeviceTLayerType.MvGenTLCXPDevice |
                DeviceTLayerType.MvGenTLXoFDevice;
        List<IDeviceInfo> devInfoList = new List<IDeviceInfo>();

        // 将以下字段声明为可为 null，以消除 CS8618 警告
        public IDevice? device;
        public IDeviceInfo deviceInfo;
        public IFloatValue? floatValue;
        public IFloatValue? gainValue;
        public IEnumValue enumValue;

        private readonly ConcurrentQueue<Bitmap> _frameQueue = new ConcurrentQueue<Bitmap>();

        private readonly AutoResetEvent _frameEvent = new AutoResetEvent(false);

        public event EventHandler<Bitmap> FrameCaptured;

        IGigEDevice gigEDevice;
        IUSBDevice usbDevice;

        IFrameOut frameOut = null; // ch:图像缓冲区 | en: Image buffer

        public bool isGrabbing = false;        // ch:是否正在取图 | en: Grabbing flag
        Thread receivedThread = null; // 接收图像线程
        IntPtr pictureBoxHandle = IntPtr.Zero; // ch:显示图像的控件句柄 | en: Control handle for image display

        private IntPtr _deviceHandle = IntPtr.Zero;

        public int _selectedIndex = 0; // 选择的设备索引

        public float _exposureTime = 1000;
        public float _gain = 10;
        public float _frameRate = 30;
        public string _enum = null;

        private readonly ILoggerService? _logger;

        public IList<IDeviceInfo> DevInfoList = new ObservableCollection<IDeviceInfo>();

        private volatile bool _isCaptureRunning = false;

        public HikCamImplement()
        {
        }

        /// <summary>
        /// 枚举并连接设备
        /// </summary>
        /// <returns></returns>
        public bool InitializeCam()
        {
            devInfoList.Clear(); // 清空设备列表
            int ret = DeviceEnumerator.EnumDevices(enumTLayerType, out devInfoList); // 枚举设备

            if (0 == devInfoList.Count)
            {
                _logger.LogWarning("No Hikvision camera found");
                return false;
            }
            if (ret != MvError.MV_OK)
            {
                ShowErrorMsg("Enum device failed", ret);
                _logger.LogError("Enum device failed");
                return false;
            }
            for (int i = 0; i < devInfoList.Count; i++)
            {
                deviceInfo = devInfoList[i];
                DevInfoList.Add(deviceInfo);                
            }

            return true;
        }

        public void OpenCam()
        {
            if (devInfoList.Count == 0)
            {
                _logger.LogWarning("No devices found.");
                return;
            }

            // 获取选择的设备信息
            deviceInfo = devInfoList[_selectedIndex];

            try
            {
                // 打开设备
                device = DeviceFactory.CreateDevice(deviceInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create device: {ex.Message}");
                MessageBox.Show($"Failed to create device: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int ret = device.Open();

            if (ret != MvError.MV_OK)
            {
                MessageBox.Show($"Open device failed: {ret}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            /*如果是网口相机*/
            if (device is IGigEDevice)
            {
                gigEDevice = device as IGigEDevice;

                /*配置网口相机的最佳包大小*/
                int packetSize;
                ret = gigEDevice.GetOptimalPacketSize(out packetSize);
                if (ret != MvError.MV_OK)
                {
                    ShowErrorMsg("Get optimal packet size failed", ret);
                    return;
                }
                else
                {
                    ret = gigEDevice.Parameters.SetIntValue("GevSCPSPacketSize", packetSize);
                    if (ret != MvError.MV_OK)
                    {
                        ShowErrorMsg("Set packet size failed", ret);
                        return;
                    }
                }

            }
            else if (device is IUSBDevice)
            {
                /*设置USB同步读写超时时间*/
                usbDevice = device as IUSBDevice;
                usbDevice.SetSyncTimeOut(1000);
            }

        }

        public void CloseCam()
        {
            if (device == null)
            {
                _logger.LogWarning("Device is not initialized.");
                return;
            }
            // 停止采集
            if (isGrabbing)
            {
                StopCapture();
            }
            // 关闭设备
            int ret = device.Close();
            if (ret != MvError.MV_OK)
            {
                ShowErrorMsg("Close device failed", ret);
                _logger.LogError("Close device failed");
            }
            else
            {
                _logger.LogInformation("Device closed successfully.");
            }
            device = null; // 清空设备引用
        }

        public void ConfigureCam()
        {
            if (device == null)
            {
                _logger.LogError("Device is not initialized.");
                return;
            }
            // 配置相机参数 设置连续采集模式
            device.Parameters.SetEnumValueByString("AcquisitionMode", "Continuous");
            device.Parameters.SetEnumValueByString("TriggerMode", "Off");

        }

        public bool SetExposure(float exposure)
        {
            _exposureTime = (float)exposure;
            device.Parameters.SetEnumValue("ExposureAuto", 0);
            int ret = device.Parameters.SetFloatValue("ExposureTime", _exposureTime);
            if (ret != MvError.MV_OK)
            {
                ShowErrorMsg("Set exposure time failed", ret);
                return false;
            }
            else
            {
                return true;
            }
        }

        public bool SetGain(float gain)
        {
            _gain = gain;
            device.Parameters.SetEnumValue("GainAuto", 0);
            int ret = device.Parameters.SetFloatValue("Gain", _gain);
            if (ret != MvError.MV_OK)
            {
                ShowErrorMsg("Set gain failed", ret);
                return false;
            }
            else
            {
                return true;
            }
        }

        public bool SetAcquisitionFrameRate(float frameRate)
        {
            _frameRate = frameRate;
            int ret = device.Parameters.SetBoolValue("AcquisitionFrameRateEnable", true);
            if (ret != MvError.MV_OK)
            {
                ShowErrorMsg("Set AcquisitionFrameRateEnable failed", ret);
                return false;
            }
            else
            {
                ret = device.Parameters.SetFloatValue("AcquisitionFrameRate", _frameRate);
                if (ret != MvError.MV_OK)
                {
                    ShowErrorMsg("Set Frame Rate Fail!", ret);
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// 连续采集
        /// </summary>
        public void StartCapture()
        {
            if (device == null)
            {
                _logger.LogError("Device is not initialized.");
                return;
            }

            device.StreamGrabber.SetImageNodeNum(9);

            // 开启抓图
            int ret = device.StreamGrabber.StartGrabbing(StreamGrabStrategy.OneByOne);
            if (ret != MvError.MV_OK)
            {
                isGrabbing = false;
                receivedThread.Join(); // 确保线程安全退出
                ShowErrorMsg("Start grabbing failed", ret);
                return;
            }
            isGrabbing = true;

            receivedThread = new Thread(ReceiveThreadProcess)
            {
                Name = "HikCameraReceiveThread",
                Priority = ThreadPriority.AboveNormal,
                IsBackground = true
            };
            receivedThread.Start(device.StreamGrabber);
            _logger.LogInformation("Started grabbing successfully.");
        }

        public void StopCapture()
        {
            if (!isGrabbing)
            {
                _logger.LogWarning("Camera is not currently grabbing.");
                return;
            }
            StopThreadProcess(); // 停止接收线程

            // 停止采集
            int ret = device.StreamGrabber.StopGrabbing();
            if (ret != MvError.MV_OK)
            {
                _logger.LogError($"Stop grabbing failed: {ret}");
                ShowErrorMsg("Stop grabbing failed", ret);
            }
            else
            {
                _logger.LogInformation("Stopped grabbing successfully.");
            }

            isGrabbing = false;

            // 清空图像队列
            while (_frameQueue.TryDequeue(out var frame))
            {
                // 清空队列中的图像
                frame.Dispose();
            }

            _logger.LogInformation("Cleared image queue and stopped capture.");

        }

        /// <summary>
        /// 保存图像到本地文件
        /// </summary>
        public void SaveCapture(string path)
        {
            int ret = device.StreamGrabber.GetImageBuffer(1000, out frameOut); // 获取一帧图像
            if (ret == MvError.MV_OK && frameOut != null)
            {
                ImageFormatInfo imageFormatInfo = new ImageFormatInfo();
                imageFormatInfo.FormatType = ImageFormatType.Bmp; // 设置图像格式为 BMP

                // 保存图像到文件
                device.ImageSaver.SaveImageToFile(path, frameOut.Image, imageFormatInfo, CFAMethod.Optimal);
                device.StreamGrabber.FreeImageBuffer(frameOut); // 释放图像缓冲区
            }
            else
            {
                _logger.LogError($"Get image buffer failed: {ret}");
                ShowErrorMsg("Get image buffer failed", ret);
            }
        }

        /// <summary>
        /// 自定义线程，接收并处理图像
        /// </summary>
        public void ReceiveThreadProcess()
        {
            _isCaptureRunning = true;
            _logger.LogInformation("Receive thread started.");
            // 轮询取图
            while (_isCaptureRunning)
            {
                int result = device.StreamGrabber.GetImageBuffer(1000, out frameOut); // 获取一帧图像
                if (frameOut == null)
                {
                    _logger.LogError("Failed to get image buffer.");
                    Thread.Sleep(100); // 等待一段时间后重试
                    continue;
                }
                if (result == MvError.MV_OK)
                {
                    // 转换为bitmap
                    var bitmap = frameOut.Image.ToBitmap();
                    if (bitmap != null)
                    {
                        _logger.LogInformation($"Received image: Width={bitmap.Width}, Height={bitmap.Height}");
                        // 这里可以添加处理图像的逻辑
                        // 例如：显示图像、保存图像等
                        _frameQueue.Enqueue(bitmap); // 将图像添加到队列中
                        _frameEvent.Set(); // 通知等待的线程有新图像到来
                        FrameCaptured?.Invoke(this, bitmap); // 触发图像捕获事件
                    }
                    else
                    {
                        _logger.LogError("Received image is null.");
                    }
                    device.StreamGrabber.FreeImageBuffer(frameOut); // 释放图像缓冲区
                }
                else
                {
                    _logger.LogError($"Get image buffer failed: {result}");
                    ShowErrorMsg("Get image buffer failed", result);
                    Thread.Sleep(50); // 等待一段时间后重试
                }
            }
        }

        public void StopThreadProcess()
        {
            _isCaptureRunning = false;
            if (receivedThread != null && receivedThread.IsAlive)
            {
                // 等待线程结束
                if (!receivedThread.Join(TimeSpan.FromSeconds(3)))
                {
                    receivedThread.Interrupt(); // 如果线程在3秒内没有结束，则强制中断
                }
                receivedThread = null; // 清空线程引用
                _logger.LogInformation("Receive thread stopped.");
            }
        }

        public string GetExposure()
        {
            // 获取相机的参数
            int ret = device.Parameters.GetFloatValue("ExposureTime", out floatValue);
            if (ret == MvError.MV_OK)
            {
                _exposureTime = floatValue.CurValue;
                _logger.LogInformation($"Exposure Time: {_exposureTime} us");
                return _exposureTime.ToString("F1");
            }
            else
            {
                return string.Empty;
            }

        }

        public bool GetExposureMax(ref double exposureMax)
        {
            return false;
        }

        public bool GetExposureMin(ref double exposureMin)
        {
            return false;
        }

        public string GetFrameRate()
        {
            int ret = device.Parameters.GetFloatValue("ResultingFrameRate", out floatValue);
            if (ret == MvError.MV_OK)
            {
                _frameRate = floatValue.CurValue;
                _logger.LogInformation($"Frame Rate: {_frameRate} fps");
                return _frameRate.ToString("F1");
            }
            else
            {
                return string.Empty;
            }

        }

        public string GetGain()
        {
            int ret = device.Parameters.GetFloatValue("Gain", out gainValue);
            if (ret == MvError.MV_OK)
            {
                _gain = gainValue.CurValue;
                _logger.LogInformation($"Gain: {_gain}");
                return _gain.ToString("F1");
            }
            else
            {
                return string.Empty;
            }
        }

        public string GetPixelFormat()
        {
            int ret = device.Parameters.GetEnumValue("PixelFormat", out enumValue);
            if (ret == MvError.MV_OK)
            {
                _enum = enumValue.CurEnumEntry.Symbolic;
                _logger.LogDebug($"Pixel Format: {_enum}");
                return _enum;
            }
            else
            {
                return string.Empty;
            }

        }

        public void Dispose()
        {
            device.Dispose();
            device = null;
        }


        private void ShowErrorMsg(string message, int errorCode)
        {
            string errorMsg;
            if (errorCode == 0)
            {
                errorMsg = message;
            }
            else
            {
                errorMsg = message + ": Error =" + String.Format("{0:X}", errorCode);
            }

            switch (errorCode)
            {
                case MvError.MV_E_HANDLE: errorMsg += " Error or invalid handle "; break;
                case MvError.MV_E_SUPPORT: errorMsg += " Not supported function "; break;
                case MvError.MV_E_BUFOVER: errorMsg += " Cache is full "; break;
                case MvError.MV_E_CALLORDER: errorMsg += " Function calling order error "; break;
                case MvError.MV_E_PARAMETER: errorMsg += " Incorrect parameter "; break;
                case MvError.MV_E_RESOURCE: errorMsg += " Applying resource failed "; break;
                case MvError.MV_E_NODATA: errorMsg += " No data "; break;
                case MvError.MV_E_PRECONDITION: errorMsg += " Precondition error, or running environment changed "; break;
                case MvError.MV_E_VERSION: errorMsg += " Version mismatches "; break;
                case MvError.MV_E_NOENOUGH_BUF: errorMsg += " Insufficient memory "; break;
                case MvError.MV_E_UNKNOW: errorMsg += " Unknown error "; break;
                case MvError.MV_E_GC_GENERIC: errorMsg += " General error "; break;
                case MvError.MV_E_GC_ACCESS: errorMsg += " Node accessing condition error "; break;
                case MvError.MV_E_ACCESS_DENIED: errorMsg += " No permission "; break;
                case MvError.MV_E_BUSY: errorMsg += " Device is busy, or network disconnected "; break;
                case MvError.MV_E_NETER: errorMsg += " Network error "; break;
            }

            MessageBox.Show(errorMsg, "PROMPT");
        }

    }

}
