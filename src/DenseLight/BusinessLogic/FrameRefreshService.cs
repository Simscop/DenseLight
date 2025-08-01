using DenseLight.Services;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows;

namespace DenseLight.BusinessLogic
{
    public class FrameRefreshService : IDisposable
    {
        private readonly ICameraService _cameraService;
        private Timer _refreshTimer;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Mat frame;

        public FrameRefreshService(ICameraService cameraService)
        {
            _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
            Initialize();
        }

        private void Initialize()
        {
            // 初始化相机
            if (!_cameraService.Init())
            {
                MessageBox.Show("相机未连接！");
                return;
            }


            // 创建定时器，每隔100毫秒刷新一次帧
            //_refreshTimer = new Timer(RefreshFrameCallback, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000));


        }

        private void RefreshFrameCallback(object? state)
        {
            try
            {
                RefreshFrame();
            }
            catch (Exception ex)
            {
                // 处理异常，例如记录日志或通知用户
                Console.WriteLine($"Error capturing frame: {ex.Message}");
            }
        }

        private void RefreshFrame()
        {
            try
            {
                // 捕获一帧图像
                if (_cameraService.Capture(out Mat newFrame) || _cameraService.StartCapture())
                {
                    frame = newFrame;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(frame)));
                }
            }
            catch (Exception ex)
            {
                // 处理异常，例如记录日志或通知用户
                Console.WriteLine($"Error capturing frame: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _refreshTimer?.Dispose();
            if (_cameraService != null)
            {
                _cameraService.Dispose();
            }
            if (frame != null)
            {
                frame.Dispose();
            }
            _refreshTimer = null;
        }
    }
}
