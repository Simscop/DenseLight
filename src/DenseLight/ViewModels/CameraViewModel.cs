using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DenseLight.BusinessLogic;
using DenseLight.Devices;
using DenseLight.Services;
using Lift.UI.Tools;
using Microsoft.WindowsAPICodePack.Dialogs;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection.Metadata;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DenseLight.ViewModels
{
    public partial class CameraViewModel : ObservableObject
    {
        private readonly ICameraService _camera;
        private readonly FrameRefreshService _frameRefreshService;
        private readonly IMessenger _messenger = WeakReferenceMessenger.Default;
        private readonly HikCameraService _hikCamera;

        private BitmapFrame? _currentFrame;

        public BitmapFrame? CurrentFrame
        {
            get => _currentFrame;
            set
            {
                if (SetProperty(ref _currentFrame, value))
                {
                    // 通知消息系统更新当前帧
                    _messenger.Send(new Message.FrameUpdateMessage(value));
                }
            }
        }


        [ObservableProperty]
        private double _exposureTime = 100; // 默认曝光时间为100ms

        [ObservableProperty]
        private double _frameRate = 30; // 默认帧率为30fps

        [ObservableProperty]
        private double _gain = 1.0; // 默认增益为1.0

        [ObservableProperty]
        private string _pixelFormat = "";

        [ObservableProperty]
        private string _root = "D:/DenseLight/Images"; // 默认保存路径

        [ObservableProperty]
        private bool _isStartCapture = false; // 是否开始捕获

        private bool _isInit;

        public CameraViewModel(ICameraService camera, IMessenger messenger, HikCameraService hikCamera)
        {
            _messenger = messenger;

            _hikCamera = hikCamera;

            _hikCamera.FrameReceived += OnFrameReceived;

            _frameRefreshService = new FrameRefreshService(camera);

            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            //_frameRefreshService = frameRefreshService ?? throw new ArgumentNullException(nameof(frameRefreshService));
            _isInit = ConfigureCamera();

            _frameRefreshService.PropertyChanged += OnFrameRefreshService_PropertyChanged;
        }

        private void OnFrameReceived(Mat frame)
        {
            try
            {
                // 将 OpenCV Mat 转换为 BitmapFrame
                BitmapFrame bitmapFrame = null;
                using (frame) // 确保 Mat 被释放
                {
                    bitmapFrame = ConvertMatToBitmapFrame(frame);
                }

                // 在 UI 线程更新
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentFrame = bitmapFrame;
                });
            }
            catch (Exception ex)
            {
                // 错误处理
            }
        }
        private BitmapFrame ConvertMatToBitmapFrame(Mat mat)
        {
            if (mat == null || mat.IsDisposed || mat.Empty())
                return null;

            // 根据 Mat 类型创建 BitmapSource
            BitmapSource source = null;

            if (mat.Channels() == 1) // 灰度图
            {
                source = BitmapSource.Create(
                    mat.Width, mat.Height, 96, 96,
                    PixelFormats.Gray8, null,
                    mat.Data, (int)mat.Step(),
                    (int)(mat.Width * mat.Channels()));
            }
            else if (mat.Channels() == 3) // BGR 彩色图
            {
                // OpenCV 是 BGR 格式，需要转换为 RGB
                using (var rgbMat = new Mat())
                {
                    Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.BGR2RGB);
                    source = BitmapSource.Create(
                        rgbMat.Width, rgbMat.Height, 96, 96,
                        PixelFormats.Rgb24, null,
                        rgbMat.Data, (int)rgbMat.Step(),
                        (int)(rgbMat.Width * rgbMat.Channels()));
                }
            }
            else if (mat.Channels() == 4) // BGRA 彩色图
            {
                source = BitmapSource.Create(
                    mat.Width, mat.Height, 96, 96,
                    PixelFormats.Bgra32, null,
                    mat.Data, (int)mat.Step(),
                    (int)(mat.Width * mat.Channels()));
            }

            // 转换为 BitmapFrame
            return source != null ? BitmapFrame.Create(source) : null;
        }

        private bool ConfigureCamera()
        {
            var isCamInit = _camera.Init();
            //_camera.Open(); // create device           

            return isCamInit;
        }

        private void OnFrameRefreshService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FrameRefreshService.frame))
            {
                CurrentFrame = _frameRefreshService.frame?.ToBitmapSource() is { } bitmap ? BitmapFrame.Create(bitmap) : null;
            }
        }

        public void Cleanup()
        {
            _frameRefreshService.PropertyChanged -= OnFrameRefreshService_PropertyChanged;
            _frameRefreshService.Dispose();
            _hikCamera.FrameReceived -= OnFrameReceived;
            CloseCamera();
        }

        [RelayCommand]
        void OpenCamera()
        {
            try
            {
                var isOpen = _camera.Open();
                if (!isOpen) { return; }
                ExposureTime = _camera.GetExposure();
                Gain = _camera.GetGain();
                FrameRate = _camera.GetFrameRate();
                PixelFormat = _camera.GetPixelFormat();
            }
            catch (Exception ex)
            {
                // 处理打开摄像头时的异常
                Console.WriteLine($"Error opening camera: {ex.Message}");
            }
        }

        [RelayCommand]
        void CloseCamera()
        {
            try
            {
                _camera.Close();
            }
            catch (Exception ex)
            {
                // 处理关闭摄像头时的异常
                Console.WriteLine($"Error closing camera: {ex.Message}");
            }
            finally
            {
                _camera.Dispose();
            }
        }

        [RelayCommand]
        void Capture()
        {
            //var isSetExposure = _camera.SetExposure((float)ExposureTime); // 这里强转可能要丢失信息
            //var isSetGain = _camera.SetGain((float)Gain);

            //if (!isSetExposure || !isSetGain)
            //{
            //    // 处理设置曝光或增益失败的情况
            //    Console.WriteLine("Failed to set exposure or gain.");
            //    return;
            //}
            if (!_isInit) { return; }

            // TODO 这里应该是保存当前显示窗口的图片，相当于截图

            _camera.Capture(out var mat);
            CurrentFrame = mat?.ToBitmapSource() is { } bitmap ? BitmapFrame.Create(bitmap) : null;
        }

        [RelayCommand]
        void SaveCapture()
        {
            if (CurrentFrame == null)
            {
                Console.WriteLine("No frame to save.");
                return;
            }
            try
            {
                // 确保目录存在
                if (string.IsNullOrEmpty(Root) || !Directory.Exists(Root))
                {
                    Directory.CreateDirectory(Root);
                }
                var path = Path.Join(Root, $"{DateTime.Now:yyyyMMdd_HH_mm_ss}.TIF");

                var image = CurrentFrame.ToMat();
                image.SaveImage(path); // 使用TIF格式保存图像
                image.Dispose(); // 释放Mat资源

                Console.WriteLine($"Frame saved to {path}");

                _camera.SaveCapture(path); // 调用相机服务保存图像
            }
            catch (Exception ex)
            {
                // 处理保存图像时的异常
                Console.WriteLine($"Error saving frame: {ex.Message}");
            }
        }

        [RelayCommand]
        void SelectRoot()
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true, // 设置为文件夹选择器
                Title = "选择保存路径",
            };

            // 显示对话框
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                Root = dialog.FileName; // 设置选中的路径
            }
        }

        [RelayCommand]
        void SetExposure()
        {
            if (_camera.SetExposure((float)ExposureTime))
            {
                Console.WriteLine($"Exposure set to {ExposureTime} ms");
            }
            else
            {
                Console.WriteLine("Failed to set exposure.");
            }
        }

        [RelayCommand]
        void SetGain()
        {
            if (_camera.SetGain((float)Gain))
            {
                Console.WriteLine($"Gain set to {Gain}");
            }
            else
            {
                Console.WriteLine("Failed to set gain.");
            }
        }

        [RelayCommand]
        void SetFrameRate()
        {
            if (_camera.SetAcquisitionFrameRate((float)FrameRate))
            {
                Console.WriteLine($"Frame rate set to {FrameRate} fps");
            }
            else
            {
                Console.WriteLine("Failed to set frame rate.");
            }
        }

        private Dispatcher Dispatcher => Application.Current.Dispatcher;

        [RelayCommand]
        void StartCapture()
        {
            IsStartCapture = _camera.StartCapture();
            if (!IsStartCapture) { MessageBox.Show("camera is not open"); }
        }

        [RelayCommand]
        void StopCapture()
        {
            _camera.StopCapture();
            IsStartCapture = false;
        }
    }
}
