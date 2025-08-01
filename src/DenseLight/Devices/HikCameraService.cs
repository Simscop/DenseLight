using DenseLight.Services;
using Lift.UI.Tools;
using MvCameraControl;
using OpenCvSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DenseLight.Devices
{
    public class HikCameraService : ICameraService, IDisposable
    {
        public event Action<BitmapSource>? ImageReady;
        private readonly ILoggerService? _logger;

        private bool _isInitialized = false;
        private bool _isGrabbing = false;
        private bool _disposed = false;

        public event EventHandler<Bitmap> FrameCaptured;

        IFrameOut frameOut = null;

        //HikCamImplement hikCam = new HikCamImplement();
        private HikCamImplement hikCam; // = new HikCamImplement();

        public IDevice? device;

        public IList<IDeviceInfo> DevInfoList = new ObservableCollection<IDeviceInfo>();

        public HikCameraService()
        {
            hikCam = new HikCamImplement(this);
        }

        //public HikCameraService(ILoggerService logger, HikCamImplement hikImp)
        //{
        //    _logger = logger;
        //    hikCam = hikImp;
        //}

        //public HikCameraService(ILoggerService logger)
        //{
        //    _logger = logger;
        //    InitializeSDK();
        //    Init();
        //    Open();
        //}

        private void InitializeSDK()
        {
            SDKSystem.Initialize();
            _isInitialized = true;
            //_logger.LogInformation("Hikvision SDK initialized successfully");
        }

        public bool Capture(out Mat mat) => hikCam.Capture(out mat);

        public double GetExposure() => hikCam.GetExposure();

        public bool GetExposureMax(ref double exposureMax)
        {
            return false;
        }

        public bool GetExposureMin(ref double exposureMin)
        {
            return false;
        }

        public double GetFrameRate() => hikCam.GetFrameRate();


        public double GetGain() => hikCam.GetGain();


        public bool Init()
        {
            InitializeSDK();
            var isInit = hikCam.InitializeCam();
            return isInit;
        }


        public void SaveCapture(string path) => hikCam.SaveCapture(path);

        public bool SetExposure(float exposure) => hikCam.SetExposure(exposure);

        public bool SetGain(float value) => hikCam.SetGain(value);

        public bool StartCapture() => hikCam.StartCapture();

        public bool StopCapture() => hikCam.StopCapture();

        public void Dispose() => hikCam.Dispose();

        public void Configure() => hikCam.ConfigureCam();

        public bool Open() => hikCam.OpenCam();

        public string GetPixelFormat() => hikCam.GetPixelFormat();

        public bool SetAcquisitionFrameRate(float value) => hikCam.SetAcquisitionFrameRate(value);

        public void Close() => hikCam.CloseCam();

        ~HikCameraService()
        {
            SDKSystem.Finalize();
        }

        // 内部事件触发方法（可添加额外处理）
        public void OnImageReady(BitmapSource image)
        {
            // 可以在这里添加预处理逻辑
            ImageReady?.Invoke(image);
        }
    }

    class HikCamImplement
    {
        private readonly HikCameraService _parent; // 父类引用        

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

        private Queue<IFrameOut> _iframeQueue = new Queue<IFrameOut>();

        private readonly AutoResetEvent _frameEvent = new AutoResetEvent(false);

        public event EventHandler<Bitmap> FrameCaptured;

        private const uint _maxQueueSize = 10;


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

        private Thread _asyncProcessThread = null;

        private Semaphore _frameGrabSem = null;

        static volatile bool _grabThreadExit = false;

        private volatile bool _processThreadExit = false;


        private volatile bool _isCaptureRunning = false;


        static void FrameGrabThread(object obj)
        {
            IStreamGrabber streamGrabber = (IStreamGrabber)obj;

            while (!_grabThreadExit)
            {
                IFrameOut frame;

                //ch：获取一帧图像 | en: Get one frame
                int ret = streamGrabber.GetImageBuffer(1000, out frame);
                if (ret != MvError.MV_OK)
                {
                    Console.WriteLine("Get Image failed:{0:x8}", ret);
                    continue;
                }

                Console.WriteLine("Get one frame: Width[{0}] , Height[{1}] , ImageSize[{2}], FrameNum[{3}]", frame.Image.Width, frame.Image.Height, frame.Image.ImageSize, frame.FrameNum);
                //Do some thing


                //ch: 释放图像缓存  | en: Release the image buffer
                streamGrabber.FreeImageBuffer(frame);
            }
        }


        public HikCamImplement(HikCameraService parent)
        {
            _parent = parent;
            _frameGrabSem = new Semaphore(0, Int32.MaxValue);
        }

        /// <summary>
        /// 枚举并连接设备
        /// </summary>
        /// <returns></returns>
        public bool InitializeCam()
        {
            _parent.DevInfoList.Clear(); // 清空设备列表
            int ret = DeviceEnumerator.EnumDevices(enumTLayerType, out devInfoList); // 枚举设备

            if (0 == devInfoList.Count)
            {
                //_logger.LogWarning("No Hikvision camera found");
                return false;
            }
            if (ret != MvError.MV_OK)
            {
                ShowErrorMsg("Enum device failed", ret);
                //_logger.LogError("Enum device failed");
                return false;
            }
            for (int i = 0; i < devInfoList.Count; i++)
            {
                deviceInfo = devInfoList[i];
                _parent.DevInfoList.Add(deviceInfo);
            }

            return true;
        }

        public bool Capture(out Mat mat)
        {
            if (_parent.device == null) { mat = null; return false; }
            int ret = _parent.device.Parameters.SetEnumValue("TriggerMode", 0); // 设置触发模式为“On”
            if (ret != MvError.MV_OK)
            {
                ShowErrorMsg("Set TriggerMode failed", ret);
                mat = null;
                return false;
            }

            _parent.device.StreamGrabber.SetImageNodeNum(5); // 设置图像节点数量

            //_processThreadExit = false; // 确保线程可以运行
            //_asyncProcessThread = new Thread(AsyncProcessThread);
            //_asyncProcessThread.Start(); // 启动异步处理线程

            //_parent.device.StreamGrabber.FrameGrabedEvent += FrameGrabedEventHandler; // 注册帧抓取事件处理程序

            ret = _parent.device.StreamGrabber.StartGrabbing();
            if (ret != MvError.MV_OK)
            {
                ShowErrorMsg("Start grabbing failed", ret);
                mat = null;
                return false;
            }

            //_processThreadExit = true; // 通知异步处理线程退出
            //_asyncProcessThread.Join(); // 等待异步处理线程结束
            var streamGrabber = _parent.device.StreamGrabber;

            Thread GrabThread = new Thread(() => 
            {
                while (!_grabThreadExit)
                {
                    IFrameOut frame;

                    //ch：获取一帧图像 | en: Get one frame
                    int ret = streamGrabber.GetImageBuffer(1000, out frame);
                    if (ret != MvError.MV_OK)
                    {
                        Console.WriteLine("Get Image failed:{0:x8}", ret);
                        continue;
                    }

                    Console.WriteLine("Get one frame: Width[{0}] , Height[{1}] , ImageSize[{2}], FrameNum[{3}]", frame.Image.Width, frame.Image.Height, frame.Image.ImageSize, frame.FrameNum);
                    //Do some thing


                    //ch: 释放图像缓存  | en: Release the image buffer
                    streamGrabber.FreeImageBuffer(frame);
                }

            });
            GrabThread.Start(_parent.device.StreamGrabber);

            _grabThreadExit = true; // 通知线程退出
            GrabThread.Join(); // 等待线程结束

            ret = _parent.device.StreamGrabber.StopGrabbing();
            if (ret != MvError.MV_OK)
            {
                ShowErrorMsg("Stop grabbing failed", ret);
                mat = null;
                return false;
            }

            //_parent.device.StreamGrabber.GetImageBuffer(1000, out frameOut);

            // 替换原有的 mat = new Mat((int)frameOut.Image.Height, (int)frameOut.Image.Width, MatType.CV_8UC3, frameOut.Image.PixelData);
            // 使用 Mat.FromPixelData 静态方法来创建 Mat 实例
            //mat = Mat.FromPixelData(
            //    (int)frameOut.Image.Height,
            //    (int)frameOut.Image.Width,
            //    MatType.CV_8UC3,
            //    frameOut.Image.PixelData
            //);
            mat = null;

            return true;
        }


        void AsyncProcessThread()
        {
            try
            {
                while (!_processThreadExit)
                {
                    if (_frameGrabSem.WaitOne(100))
                    {
                        IFrameOut frame = _iframeQueue.Dequeue();
                        Console.WriteLine("AsyncProcessThread: process one frame, Width[{0}] , Height[{1}] , FrameNum[{2}]", frame.Image.Width, frame.Image.Height, frame.FrameNum);

                        //Processing the image data, such as algorithms

                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("AsyncProcessThread exception: " + e.Message);
            }

        }

        void FrameGrabedEventHandler(object sender, FrameGrabbedEventArgs e)
        {
            Console.WriteLine("FrameGrabedEventHandler: Get one frame, Width[{0}] , Height[{1}] , FrameNum[{2}]", e.FrameOut.Image.Width, e.FrameOut.Image.Height, e.FrameOut.FrameNum);

            try
            {

                lock (this)
                {
                    if (_iframeQueue.Count <= _maxQueueSize)
                    {
                        // ch: 克隆图像数据（深拷贝） | en :Clone frame data using deep copy
                        IFrameOut frameCopy = (IFrameOut)e.FrameOut.Clone();

                        // 转换为BitmapSource
                        BitmapSource bitmapSource = ConvertToBitmapSource(frameCopy);

                        //ch: 添加到队列并通知处理线程 | en: Add the frame to the queue and notify the processing thread
                        _iframeQueue.Enqueue(frameCopy);
                        _frameGrabSem.Release();

                        _parent.OnImageReady(bitmapSource); // 通知父类图像已准备好
                    }

                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("FrameGrabedEventHandler exception: " + exception.Message);
            }

        }


        public static BitmapSource ConvertToBitmapSource(IFrameOut frame)
        {
            if (frame == null || frame.Image == null || frame.Image.PixelData == null)
                return null;

            int width = (int)frame.Image.Width;
            int height = (int)frame.Image.Height;
            int stride = width * (frame.Image.PixelType == MvGvspPixelType.PixelType_Gvsp_HB_Mono8 ? 1 : 3); // 根据像素类型计算步长

            BitmapSource bitmapSource = null;

            // 根据像素类型选择合适的格式
            if (frame.Image.PixelType == MvGvspPixelType.PixelType_Gvsp_HB_Mono8)
            {
                bitmapSource = BitmapSource.Create(
                    width, height, 96, 96, // DPI
                    PixelFormats.Gray8, // 灰度图像格式
                    null, // 调色板
                    frame.Image.PixelData, // 图像数据
                    stride);
            }
            else if (frame.Image.PixelType == MvGvspPixelType.PixelType_Gvsp_HB_Mono8)
            {
                bitmapSource = BitmapSource.Create(
                    width, height, 96, 96,
                    PixelFormats.Rgb24, // RGB格式
                    null,
                    frame.Image.PixelData,
                    stride);
            }
            // 其他格式（如BGR、YUV等）可能需要额外的转换逻辑

            return bitmapSource;
        }

        public bool OpenCam()
        {
            if (_parent.DevInfoList.Count == 0)
            {
                //_logger.LogWarning("No devices found.");
                return false;
            }

            // 获取选择的设备信息
            deviceInfo = _parent.DevInfoList[_selectedIndex];

            try
            {
                // 打开设备
                device = DeviceFactory.CreateDevice(deviceInfo);
                _parent.device = device;
            }
            catch (Exception ex)
            {
                //_logger.LogError($"Failed to create device: {ex.Message}");
                MessageBox.Show($"Failed to create device: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            int ret = _parent.device.Open();

            if (ret != MvError.MV_OK)
            {
                MessageBox.Show($"Open device failed: {ret}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
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
                    return false;
                }
                else
                {
                    ret = gigEDevice.Parameters.SetIntValue("GevSCPSPacketSize", packetSize);
                    if (ret != MvError.MV_OK)
                    {
                        ShowErrorMsg("Set packet size failed", ret);
                        return false;
                    }
                    _parent.device = gigEDevice;
                }
            }
            else if (device is IUSBDevice)
            {
                /*设置USB同步读写超时时间*/
                usbDevice = device as IUSBDevice;
                usbDevice.SetSyncTimeOut(1000);
                _parent.device = usbDevice;
            }

            return true;

        }

        public void CloseCam()
        {
            if (_parent.device == null)
            {
                //_logger.LogWarning("Device is not initialized.");
                return;
            }
            // 停止采集
            if (isGrabbing)
            {
                StopCapture();
            }
            // 关闭设备
            int ret = _parent.device.Close();
            if (ret != MvError.MV_OK)
            {
                ShowErrorMsg("Close device failed", ret);
                //_logger.LogError("Close device failed");
            }
            else
            {
                //_logger.LogInformation("Device closed successfully.");
            }
            //_parent.device = null; // 清空设备引用
        }

        public void ConfigureCam()
        {
            if (_parent.device == null)
            {
                //_logger.LogError("Device is not initialized.");
                return;
            }
            // 配置相机参数 设置连续采集模式
            _parent.device.Parameters.SetEnumValueByString("AcquisitionMode", "Continuous");
            _parent.device.Parameters.SetEnumValueByString("TriggerMode", "Off");

        }

        public bool SetExposure(float exposure)
        {
            _exposureTime = (float)exposure;
            _parent.device.Parameters.SetEnumValue("ExposureAuto", 0);
            int ret = _parent.device.Parameters.SetFloatValue("ExposureTime", _exposureTime);
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
            //device = _parent.device;
            _gain = gain;
            _parent.device.Parameters.SetEnumValue("GainAuto", 0);
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
            int ret = _parent.device.Parameters.SetBoolValue("AcquisitionFrameRateEnable", true);
            if (ret != MvError.MV_OK)
            {
                ShowErrorMsg("Set AcquisitionFrameRateEnable failed", ret);
                return false;
            }
            else
            {
                ret = _parent.device.Parameters.SetFloatValue("AcquisitionFrameRate", _frameRate);
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
        public bool StartCapture()
        {
            if (_parent.device == null)
            {
                //_logger.LogError("Device is not initialized.");
                return false;
            }
            int ret = _parent.device.Parameters.SetEnumValue("TriggerMode", 0); // 设置触发模式为“On”
            if (ret != MvError.MV_OK)
            {
                ShowErrorMsg("Set TriggerMode failed", ret);               
                return false;
            }

            _parent.device.StreamGrabber.SetImageNodeNum(9);

            // 开启抓图
            ret = _parent.device.StreamGrabber.StartGrabbing(StreamGrabStrategy.OneByOne);
            if (ret != MvError.MV_OK)
            {
                isGrabbing = false;
                receivedThread.Join(); // 确保线程安全退出
                ShowErrorMsg("Start grabbing failed", ret);
                return false;
            }
            isGrabbing = true;

            var streamGrabber = _parent.device.StreamGrabber;

            receivedThread = new Thread(()=>
            {
                while (!_grabThreadExit)
                {
                    IFrameOut frame;

                    //ch：获取一帧图像 | en: Get one frame
                    int ret = streamGrabber.GetImageBuffer(1000, out frame);
                    if (ret != MvError.MV_OK)
                    {
                        Console.WriteLine("Get Image failed:{0:x8}", ret);
                        continue;
                    }

                    Console.WriteLine("Get one frame: Width[{0}] , Height[{1}] , ImageSize[{2}], FrameNum[{3}]", frame.Image.Width, frame.Image.Height, frame.Image.ImageSize, frame.FrameNum);
                    //Do some thing


                    //ch: 释放图像缓存  | en: Release the image buffer
                    streamGrabber.FreeImageBuffer(frame);
                }

            }) // ReceiveThreadProcess
            {
                Name = "HikCameraReceiveThread",
                Priority = ThreadPriority.AboveNormal,
                IsBackground = true
            };
            receivedThread.Start();


            //_logger.LogInformation("Started grabbing successfully.");
            return true;
        }

        public bool StopCapture()
        {
            if (!isGrabbing)
            {
                //_logger.LogWarning("Camera is not currently grabbing.");
                return false;
            }
            StopThreadProcess(); // 停止接收线程

            // 停止采集
            int ret = _parent.device.StreamGrabber.StopGrabbing();
            if (ret != MvError.MV_OK)
            {
                //_logger.LogError($"Stop grabbing failed: {ret}");
                ShowErrorMsg("Stop grabbing failed", ret);
                return false;
            }
            else
            {
                //_logger.LogInformation("Stopped grabbing successfully.");

            }

            isGrabbing = false;

            // 清空图像队列
            while (_frameQueue.TryDequeue(out var frame))
            {
                // 清空队列中的图像
                frame.Dispose();
            }

            //_logger.LogInformation("Cleared image queue and stopped capture.");
            return true;
        }

        /// <summary>
        /// 保存图像到本地文件
        /// </summary>
        public void SaveCapture(string path)
        {
            int ret = _parent.device.StreamGrabber.GetImageBuffer(1000, out frameOut); // 获取一帧图像
            if (ret == MvError.MV_OK && frameOut != null)
            {
                ImageFormatInfo imageFormatInfo = new ImageFormatInfo();
                imageFormatInfo.FormatType = ImageFormatType.Bmp; // 设置图像格式为 BMP

                // 保存图像到文件
                _parent.device.ImageSaver.SaveImageToFile(path, frameOut.Image, imageFormatInfo, CFAMethod.Optimal);
                _parent.device.StreamGrabber.FreeImageBuffer(frameOut); // 释放图像缓冲区
            }
            else
            {
                //_logger.LogError($"Get image buffer failed: {ret}");
                ShowErrorMsg("Get image buffer failed", ret);
            }
        }




        public void ReceiveThreadProcess()
        {
            _isCaptureRunning = true;
            //_logger.LogInformation("Receive thread started.");
            // 轮询取图
            while (_isCaptureRunning)
            {
                int result = _parent.device.StreamGrabber.GetImageBuffer(1000, out frameOut); // 获取一帧图像
                if (frameOut == null)
                {
                    //_logger.LogError("Failed to get image buffer.");
                    Thread.Sleep(100); // 等待一段时间后重试
                    continue;
                }
                if (result == MvError.MV_OK)
                {
                    // 转换为bitmap
                    using (var bitmap = frameOut.Image.ToBitmap())
                    {
                        if (bitmap != null)
                        {
                            //_logger.LogInformation($"Received image: Width={bitmap.Width}, Height={bitmap.Height}");
                            // 这里可以添加处理图像的逻辑
                            // 例如：显示图像、保存图像等
                            _frameQueue.Enqueue(bitmap); // 将图像添加到队列中
                            _frameEvent.Set(); // 通知等待的线程有新图像到来
                            FrameCaptured?.Invoke(this, bitmap); // 触发图像捕获事件

                            var bitmapSource = ConvertToBitmapSource(bitmap);

                            _parent.OnImageReady(bitmapSource); // 通知父类图像已准备好
                        }
                        else
                        {
                            //_logger.LogError("Received image is null.");
                        }
                        _parent.device.StreamGrabber.FreeImageBuffer(frameOut); // 释放图像缓冲区
                    }

                }
                else
                {
                    //_logger.LogError($"Get image buffer failed: {result}");
                    ShowErrorMsg("Get image buffer failed", result);
                    Thread.Sleep(50); // 等待一段时间后重试
                }
            }

        }



        // 将System.Drawing.Bitmap转换为BitmapSource
        private BitmapSource ConvertToBitmapSource(Bitmap bitmap)
        {
            using (var memoryStream = new MemoryStream())
            {
                // 保存为BMP格式避免GDI+锁问题
                bitmap.Save(memoryStream, ImageFormat.Bmp);
                memoryStream.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // 重要：使图像跨线程安全

                return bitmapImage;
            }
        }

        public void StopThreadProcess()
        {
            _isCaptureRunning = false;
            _grabThreadExit = true; // 通知线程退出

            if (receivedThread != null && receivedThread.IsAlive)
            {
                // 等待线程结束
                if (!receivedThread.Join(TimeSpan.FromSeconds(3)))
                {
                    receivedThread.Interrupt(); // 如果线程在3秒内没有结束，则强制中断
                }
                receivedThread = null; // 清空线程引用
                //_logger.LogInformation("Receive thread stopped.");
            }
        }

        public double GetExposure()
        {
            // 获取相机的参数
            int ret = _parent.device.Parameters.GetFloatValue("ExposureTime", out floatValue);
            if (ret == MvError.MV_OK)
            {
                _exposureTime = floatValue.CurValue;
                //_logger.LogInformation($"Exposure Time: {_exposureTime} us");
                return _exposureTime;
            }
            else
            {
                return -1;
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

        public double GetFrameRate()
        {
            int ret = _parent.device.Parameters.GetFloatValue("ResultingFrameRate", out floatValue);
            if (ret == MvError.MV_OK)
            {
                _frameRate = floatValue.CurValue;
                //_logger.LogInformation($"Frame Rate: {_frameRate} fps");
                return _frameRate;
            }
            else
            {
                return -1;
            }

        }

        public double GetGain()
        {
            int ret = _parent.device.Parameters.GetFloatValue("Gain", out gainValue);
            if (ret == MvError.MV_OK)
            {
                _gain = gainValue.CurValue;
                //_logger.LogInformation($"Gain: {_gain}");
                return _gain;
            }
            else
            {
                return -1;
            }
        }

        public string GetPixelFormat()
        {
            int ret = _parent.device.Parameters.GetEnumValue("PixelFormat", out enumValue);
            if (ret == MvError.MV_OK)
            {
                _enum = enumValue.CurEnumEntry.Symbolic;
                //_logger.LogDebug($"Pixel Format: {_enum}");
                return _enum;
            }
            else
            {
                return string.Empty;
            }

        }

        public void Dispose()
        {
            if (_parent.device == null) return;
            _parent.device.Dispose();
            _parent.device = null;

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
