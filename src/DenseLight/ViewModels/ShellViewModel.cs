using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DenseLight.BusinessLogic;
using DenseLight.Devices;
using DenseLight.Logger;
using DenseLight.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Zaber.Motion.Ascii;
using OpenCvSharp;
using DenseLight.Models;
using OpenCvSharp.WpfExtensions;

namespace DenseLight.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private SteerViewModel _motor;
    private CameraViewModel _hikCamera;
    private ICameraService _cameraService;
    private AutoFocusService _autoFocusService;
    private ILoggerService _logger;
    private IImageProcessingService _imageProcessingService;
    private ZaberMotorService _zaberMotorService;

    //private VideoProcessingService _videoProcessing;
    private Dispatcher _dispatcher;
    private WriteableBitmap _currentFrame;

    private readonly IMessenger _messenger = WeakReferenceMessenger.Default;


    [ObservableProperty] private string _errorMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _coms;

    [ObservableProperty] private string _com = "";

    [ObservableProperty]
    private double _focusScore;

    [ObservableProperty]
    private double _actualFps;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private int _targetFps = 30;

    [ObservableProperty]
    private double _cropSize = 0.8;

    [ObservableProperty]
    private bool _isAutoFocusRunning;

    public WriteableBitmap CurrentFrame
    {
        get => _currentFrame;
        set => SetProperty(ref _currentFrame, value);
    }

    [ObservableProperty]
    private BitmapFrame? _cameraImage;

    private readonly IImageProcessingService _imageProcessing;
    private CancellationTokenSource _autoFocusCts;

    private HikCameraService _hikCam;

    private Mat mat;


    public ShellViewModel(IMessenger messenger)
    {
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = new FileLoggerService();
        _motor = App.Current.Services.GetRequiredService<SteerViewModel>();
        //_hikCamera = App.Current.Services.GetRequiredService<CameraViewModel>();
        //_cameraService = new HikCameraService(_logger);
        //_videoProcessing = new VideoProcessingService(_cameraService, _logger, _imageProcessing);

        //_hikCam = hikCamera ?? throw new ArgumentNullException(nameof(hikCamera));

        _dispatcher = Dispatcher.CurrentDispatcher;

        // 注册事件
        //_videoProcessing.FrameProcessed += OnFrameProcessed;
        //_videoProcessing.FocusScoreUpdated += OnFocusScoreUpdated;
        // 启动定期更新对焦分数
        //StartFocusScoreUpdate();


        WeakReferenceMessenger.Default.Register<DisplayFrame, string>(this, "Display", (sender, message) =>
        {
            Application.Current.Dispatcher.BeginInvoke(() =>  // 异步更新，避免阻塞线程
            {
                if (message?.Image != null)
                {
                    using (var receivedFrame = message.Image)
                    {
                        using (var cloned = receivedFrame.Clone())
                        {
                            var bitmapSource = cloned?.ToBitmapSource(); // 不是深拷贝

                            CameraImage = BitmapFrame.Create(bitmapSource);

                        }

                    }
                }
                else
                {
                    CameraImage = null;
                    return;
                }
            });
        });

        #region 相机

        //[RelayCommand]
        //private void StartVideo()
        //{
        //    if (_isProcessing) return;

        //    try
        //    {
        //        _videoProcessing.StartProcessing(_targetFps);
        //        IsProcessing = true;
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Failed to start video: {ex.Message}", "Error",
        //            MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}

        //[RelayCommand]
        //private void StopVideo()
        //{
        //    if (!_isProcessing) return;

        //    _videoProcessing.StopProcessing();
        //    IsProcessing = false;
        //}

        //[RelayCommand]
        //private void UpdateFrameRate()
        //{
        //    if (_isProcessing)
        //    {
        //        _videoProcessing.SetFrameRate(_targetFps);
        //    }
        //}

        //private void OnFocusScoreUpdated(object? sender, double score)
        //{
        //    _dispatcher.Invoke(() =>
        //    {
        //        FocusScore = score;
        //    });
        //}

        //private void OnFrameProcessed(object? sender, Bitmap bitmap)
        //{
        //    // 在UI线程更新图像
        //    _dispatcher.Invoke(() =>
        //    {
        //        try
        //        {
        //            // 在UI线程更新图像
        //            // 创建或更新WriteableBitmap
        //            if (CurrentFrame == null ||
        //                CurrentFrame.PixelWidth != bitmap.Width ||
        //                CurrentFrame.PixelHeight != bitmap.Height)
        //            {
        //                CurrentFrame = new WriteableBitmap(
        //                    bitmap.Width,
        //                    bitmap.Height,
        //                    96, 96,
        //                    System.Windows.Media.PixelFormats.Bgr24,
        //                    null);
        //            }

        //            // 锁定位图
        //            CurrentFrame.Lock();

        //            // 将Bitmap数据复制到WriteableBitmap
        //            var sourceData = bitmap.LockBits(
        //                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
        //                System.Drawing.Imaging.ImageLockMode.ReadOnly,
        //                System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        //            CurrentFrame.WritePixels(
        //                new Int32Rect(0, 0, bitmap.Width, bitmap.Height),
        //                sourceData.Scan0,
        //                bitmap.Width * bitmap.Height * 3,
        //                bitmap.Width * 3);

        //            bitmap.UnlockBits(sourceData);
        //            CurrentFrame.Unlock();
        //        }
        //        catch (Exception ex)
        //        {
        //            // 处理图像显示错误
        //        }
        //        finally
        //        {
        //            bitmap.Dispose();
        //        }
        //    });
        //}

        //private async void StartFocusScoreUpdate()
        //{
        //    while (true)
        //    {
        //        if (!IsAutoFocusRunning)
        //        {
        //            try
        //            {
        //                _cameraService.Capture(out var mat);
        //                using (var image = mat)
        //                {
        //                    FocusScore = _imageProcessing.CalculateFocusScore(image, CropSize);
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogException(ex, "Error updating focus score");
        //            }
        //        }
        //        await Task.Delay(1000); // 每秒更新一次
        //    }
        //}


        //[RelayCommand]
        //private void CancelAutoFocus()
        //{
        //    _autoFocusCts?.Cancel();
        //}


        #endregion


        //public void Dispose()
        //{

        //    _videoProcessing.FrameProcessed -= OnFrameProcessed;
        //    _videoProcessing.FocusScoreUpdated -= OnFocusScoreUpdated;
        //    _videoProcessing.Dispose();
        //}

    }

}