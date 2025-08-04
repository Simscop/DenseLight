using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DenseLight.BusinessLogic;
using DenseLight.Devices;
using DenseLight.Models;
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

        private DateTime _lastUpdate = DateTime.MinValue;

        private Mat _frame;

        public Mat Frame
        {
            get { return _frame; }
            set { SetProperty(ref _frame, value); }
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
        private volatile bool _isInit = false;

        [ObservableProperty]
        private volatile bool _isCamOpen = false;

        [ObservableProperty]
        private volatile bool _isClosed = false;

        [ObservableProperty]
        private volatile bool _CanAcquisition = false;

        [ObservableProperty]
        private volatile bool _IsAcquisition = false;

        public CameraViewModel(ICameraService camera, IMessenger messenger)
        {
            _messenger = messenger;
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));

            _camera.FrameReceived += OnFrameReceived;

            IsInit = ConfigureCamera();
        }


        private void OnFrameReceived(Mat frame)
        {
            try
            {
                var cloneFrame = frame.Clone();

                using (frame) // 确保原始frame在方法结束时dispose（即使异常）
                {
                    Task.Run(() =>
                    {
                        WeakReferenceMessenger.Default.Send<DisplayFrame, string>(new DisplayFrame()
                        {
                            Image = cloneFrame,
                        }, "Display");
                    });
                }
            }
            catch (Exception ex)
            {
                // 错误处理
                frame.Dispose();
            }
        }

        private WriteableBitmap _writeableBitmap;

        //private unsafe void UpdateImage(Mat frame)
        //{
        //    if (frame == null || frame.IsDisposed || frame.Empty()) return;

        //    try
        //    {
        //        int width = frame.Width;
        //        int height = frame.Height;
        //        int channels = frame.Channels();

        //        System.Windows.Media.PixelFormat format = PixelFormats.Bgr24;
        //        if (channels == 1) format = PixelFormats.Gray8;
        //        else if (channels == 4) format = PixelFormats.Bgra32;

        //        if (_writeableBitmap == null || _writeableBitmap.PixelWidth != width || _writeableBitmap.PixelHeight != height || _writeableBitmap.Format != format)
        //        {
        //            _writeableBitmap = new WriteableBitmap(width, height, 96, 96, format, null);
        //        }

        //        _writeableBitmap.Lock();
        //        try
        //        {
        //            int bufferSize = height * (int)frame.Step();

        //            Buffer.MemoryCopy((void*)frame.Data,
        //                (void*)_writeableBitmap.BackBuffer, bufferSize, bufferSize);

        //            _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        //        }
        //        finally
        //        {
        //            _writeableBitmap.Unlock();
        //        }

        //        CurrentFrame = BitmapFrame.Create(_writeableBitmap);


        //    }
        //    catch { }
        //    finally
        //    {
        //        frame.Dispose();
        //    }

        //}

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
            if (isCamInit) { IsCamOpen = true; }

            return isCamInit;
        }

        //private void OnFrameRefreshService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        //{
        //    if (e.PropertyName == nameof(FrameRefreshService.frame))
        //    {
        //        CurrentFrame = _frameRefreshService.frame?.ToBitmapSource() is { } bitmap ? BitmapFrame.Create(bitmap) : null;
        //    }
        //}

        public void Cleanup()
        {
            //_frameRefreshService.PropertyChanged -= OnFrameRefreshService_PropertyChanged;
            _frameRefreshService.Dispose();
            _hikCamera.FrameReceived -= OnFrameReceived;
            _writeableBitmap = null;
            CloseCamera();
        }

        [RelayCommand]
        void OpenCamera()
        {
            if (IsInit)
            {
                var isOpen = _camera.Open(); // create device
                if (!isOpen) { MessageBox.Show("相机打开失败，请检查相机是否连接"); return; }
                ExposureTime = _camera.GetExposure();
                Gain = _camera.GetGain();
                FrameRate = _camera.GetFrameRate();
                PixelFormat = _camera.GetPixelFormat();
                IsClosed = true;
                CanAcquisition = true;
                IsCamOpen = false;
            }
            else { MessageBox.Show("相机未正确初始化"); }
        }

        [RelayCommand]
        void CloseCamera()
        {
            try
            {
                _camera.Close();
                IsCamOpen = true;
                CanAcquisition = false;
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
            //CurrentFrame = mat?.ToBitmapSource() is { } bitmap ? BitmapFrame.Create(bitmap) : null;
        }

        [RelayCommand]
        void SaveCapture()
        {
            if (Frame == null)
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

                var image = Frame;
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
            if (IsInit && CanAcquisition)
            {
                Task.Run(() =>
                {
                    IsAcquisition = _camera.StartCapture();
                    if (!IsAcquisition) { MessageBox.Show("相机视频流采集失败"); }
                    IsAcquisition = true;
                    IsClosed = false;
                });
            }
            else if (IsAcquisition)
            {
                IsAcquisition = !_camera.StopCapture();
            }
            else { }
        }

        [RelayCommand]
        void StopCapture()
        {
            _camera.StopCapture();
            IsAcquisition = false;
            CanAcquisition = true;
            IsClosed = true;
        }
    }
}
