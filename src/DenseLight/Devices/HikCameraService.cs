using DenseLight.Services;
using OpenCvSharp;
using MvCameraControl;
using System.Runtime.InteropServices;
using System.Windows.Navigation;

namespace DenseLight.Devices
{
    public class HikCameraService : ICameraService, IDisposable
    {
        private readonly ILoggerService _logger;
        private IntPtr _deviceHandle = IntPtr.Zero;
        private bool _isInitialized = false;
        private bool _isGrabbing = false;
        private bool _disposed = false;

        HikCamImplement hikCam = new HikCamImplement();

        public HikCameraService()
        {
            InitializeSDK();
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
            hikCam.device.StreamGrabber.GetImageBuffer(1000, out IFrameOut frameOut);

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


        public bool SaveCapture(string path)
        {
            return false;
        }

        public bool SetExposure(double exposure)
        {
            return false;
        }


        public bool SetGain(int value)
        {
            return false;
        }

        public bool StartCapture()
        {
            hikCam.StartCapture();
            return true;
        }


        public bool StopCapture()
        {
            hikCam.StopCapture();
            return false;
        }

        public void Dispose()
        {
            SDKSystem.Finalize();
        }

        public void Configure() => hikCam.ConfigureCam();


        public void Open() => hikCam.OpenCam();


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

        public IDevice device;
        public IFloatValue floatValue;
        public IIntValue gainValue;

        bool isGrabbing = false;        // ch:是否正在取图 | en: Grabbing flag
        Thread receivedThread = null; // 接收图像线程
        IntPtr pictureBoxHandle = IntPtr.Zero; // ch:显示图像的控件句柄 | en: Control handle for image display


        public float _exposureTime = 1000;
        public long _gain = 10;
        public float _frameRate = 30;


        public HikCamImplement()
        {
            // 初始化相机参数
            _exposureTime = 1000; // 默认曝光时间为1000毫秒
            _gain = 10; // 默认增益为10
            _frameRate = 30; // 默认帧率为30 FPS
        }
        public bool InitializeCam()
        {
            // Open 枚举相机

            int ret = DeviceEnumerator.EnumDevices(enumTLayerType, out devInfoList);

            if (0 == devInfoList.Count)
            {
                return false;
            }


            if (ret == MvError.MV_OK)
            {
                Console.WriteLine("Enum device count : {0}", devInfoList.Count);
                device = DeviceFactory.CreateDevice(devInfoList[0]); // 默认第一台
                return true;
            }
            else
            {
                Console.WriteLine("相机未识别！");
                return false;
            }

        }

        public void OpenCam()
        {

            // 打开相机
            int ret = device.Open();

            if (ret != MvError.MV_OK)
            {
                Console.WriteLine("Open device fail: {0}", ret);
                return;
            }
            else
            {
                Console.WriteLine("Open device success!");
                //TODO：配置相机参数等操作            

            }

            /*如果是网口相机*/
            if (device is IGigEDevice)
            {
                IGigEDevice gigEDevice = device as IGigEDevice;

                /*配置网口相机的最佳包大小*/
                int packetSize;
                gigEDevice.GetOptimalPacketSize(out packetSize);
                gigEDevice.Parameters.SetIntValue("GevSCPSPacketSize", packetSize);
            }
            else if (device is IUSBDevice)
            {
                /*设置USB同步读写超时时间*/
                IUSBDevice usbDevice = device as IUSBDevice;
                usbDevice.SetSyncTimeOut(1000);
            }


        }

        public void ConfigureCam()
        {
            if (device == null)
            {
                Console.WriteLine("Device is not initialized.");
                return;
            }
            // 配置相机参数
            // 例如设置曝光时间、增益等
            device.Parameters.SetFloatValue("ExposureTime", _exposureTime); // 设置曝光时间为10毫秒
            device.Parameters.SetIntValue("Gain", _gain); // 设置增益为10
            device.Parameters.SetFloatValue("FrameRate", _frameRate); // 设置帧率为30 FPS

            device.Parameters.SetEnumValue("PixelFormat", 255);

            Console.WriteLine("Camera configured successfully.");
        }



        /// <summary>
        /// 连续采集
        /// </summary>
        public void StartCapture()
        {
            if (device == null)
            {
                Console.WriteLine("Device is not initialized.");
                return;
            }

            // 开始采集
            int ret = device.Parameters.SetEnumValueByString("TriggerMode", "Off");
            if (ret != MvError.MV_OK)
            {
                Console.WriteLine("Start capturing failed: {0}", ret);
            }
            else
            {
                Console.WriteLine("Started capturing successfully.");
            }

            // 连续采集
            ret = device.Parameters.SetStringValue("AcquisitionMode", "Continuous");
            if (ret != MvError.MV_OK)
            {
                Console.WriteLine("Set acquisition mode failed: {0}", ret);
            }
            else
            {
                Console.WriteLine("Acquisition mode set to continuous.");
            }


            device.StreamGrabber.SetImageNodeNum(9);

            // 开启抓图
            ret = device.StreamGrabber.StartGrabbing(StreamGrabStrategy.OneByOne);
            if (ret != MvError.MV_OK)
            {
                Console.WriteLine("Start grabbing failed: {0}", ret);
                return;
            }
            isGrabbing = true;
            // 开启线程抓图

            receivedThread = new Thread(ReceiveThreadProcess);
            receivedThread.Start(device.StreamGrabber);

        }

        public void StopCapture()
        {
            isGrabbing = false;
            if (device == null)
            {
                Console.WriteLine("Device is not initialized.");
                return;
            }
            // 停止采集
            int ret = device.StreamGrabber.StopGrabbing();
            if (ret != MvError.MV_OK)
            {
                Console.WriteLine("Stop grabbing failed: {0}", ret);
            }
            else
            {
                Console.WriteLine("Stopped grabbing successfully.");
            }

            // 等待接收线程结束
            if (receivedThread != null && receivedThread.IsAlive)
            {
                receivedThread.Join(); // 等待采集线程结束并安全退出
                receivedThread = null;
            }
        }

        /// <summary>
        /// 自定义线程，接收并处理图像
        /// </summary>
        public void ReceiveThreadProcess()
        {
            IFrameOut frameOut = null;
            int result = MvError.MV_OK;
            // 轮询取图
            while (isGrabbing)
            {
                result = device.StreamGrabber.GetImageBuffer(1000, out frameOut); // 超时1000 ms
                if (result == MvError.MV_OK)
                {
                    // 成功获取图像缓冲区
                    device.ImageRender.DisplayOneFrame(pictureBoxHandle, frameOut.Image);
                    // 释放图像缓冲区
                    device.StreamGrabber.FreeImageBuffer(frameOut);
                }
            }
        }

        public string GetExposure()
        {
            int ret = device.Parameters.GetFloatValue("ExposureTime", out floatValue);
            if (ret != MvError.MV_OK)
            {
                Console.WriteLine("Get exposure failed: {0}", ret);
                return string.Empty;
            }
            return floatValue.CurValue.ToString("F1");
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
            int ret = device.Parameters.GetFloatValue("FrameRate", out floatValue);
            if (ret != MvError.MV_OK)
            {
                Console.WriteLine("Get frame rate failed: {0}", ret);
                return string.Empty;
            }
            return floatValue.CurValue.ToString("F1");
        }

        public string GetGain()
        {
            int ret = device.Parameters.GetIntValue("Gain", out IIntValue gainValue);
            if (ret != MvError.MV_OK)
            {
                Console.WriteLine("Get gain failed: {0}", ret);
                return string.Empty;
            }
            return gainValue.CurValue.ToString("F1");
        }

    }

}
