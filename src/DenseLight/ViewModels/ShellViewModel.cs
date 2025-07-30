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
using Zaber.Motion.Ascii;

namespace DenseLight.ViewModels;

public partial class ShellViewModel : ObservableObject, IDisposable
{
    private  IMotor _motor;
    private  ICameraService _cameraService;
    private  AutoFocusService _autoFocusService;
    private ILoggerService _logger;
    private  IImageProcessingService _imageProcessingService;
    private ZaberMotorService _zaberMotorService;

    private VideoProcessingService _videoProcessing;
    private  Dispatcher _dispatcher;
    private WriteableBitmap _currentFrame;

    [ObservableProperty] private double _currentX;
    [ObservableProperty] private double _currentY;
    [ObservableProperty] private double _currentZ;

    [ObservableProperty] private string _connectionStatus = "Disconnected";
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


    private readonly IImageProcessingService _imageProcessing;
    private CancellationTokenSource _autoFocusCts;

    public ShellViewModel()
    {
        _logger = new FileLoggerService();
        _motor = new ZaberMotorService();
        _cameraService = new HikCameraService(_logger);
        _videoProcessing = new VideoProcessingService(_cameraService, _logger, _imageProcessing);
       
        _dispatcher = Dispatcher.CurrentDispatcher;

        // 注册事件
        _videoProcessing.FrameProcessed += OnFrameProcessed;
        _videoProcessing.FocusScoreUpdated += OnFocusScoreUpdated;

        InitializeMotor();
        // 启动定期更新对焦分数
        //StartFocusScoreUpdate();

        Coms = new ObservableCollection<string>(SerialPort.GetPortNames());
    }

    #region 相机

    [RelayCommand]
    private void StartVideo()
    {
        if (_isProcessing) return;

        try
        {
            _videoProcessing.StartProcessing(_targetFps);
            IsProcessing = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start video: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void StopVideo()
    {
        if (!_isProcessing) return;

        _videoProcessing.StopProcessing();
        IsProcessing = false;
    }

    [RelayCommand]
    private void UpdateFrameRate()
    {
        if (_isProcessing)
        {
            _videoProcessing.SetFrameRate(_targetFps);
        }
    }

    private void OnFocusScoreUpdated(object? sender, double score)
    {
        _dispatcher.Invoke(() =>
        {
            FocusScore = score;
        });
    }

    private void OnFrameProcessed(object? sender, Bitmap bitmap)
    {
        // 在UI线程更新图像
        _dispatcher.Invoke(() =>
        {
            try
            {
                // 在UI线程更新图像
                // 创建或更新WriteableBitmap
                if (CurrentFrame == null ||
                    CurrentFrame.PixelWidth != bitmap.Width ||
                    CurrentFrame.PixelHeight != bitmap.Height)
                {
                    CurrentFrame = new WriteableBitmap(
                        bitmap.Width,
                        bitmap.Height,
                        96, 96,
                        System.Windows.Media.PixelFormats.Bgr24,
                        null);
                }

                // 锁定位图
                CurrentFrame.Lock();

                // 将Bitmap数据复制到WriteableBitmap
                var sourceData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                CurrentFrame.WritePixels(
                    new Int32Rect(0, 0, bitmap.Width, bitmap.Height),
                    sourceData.Scan0,
                    bitmap.Width * bitmap.Height * 3,
                    bitmap.Width * 3);

                bitmap.UnlockBits(sourceData);
                CurrentFrame.Unlock();
            }
            catch (Exception ex)
            {
                // 处理图像显示错误
            }
            finally
            {
                bitmap.Dispose();
            }
        });
    }

    private async void StartFocusScoreUpdate()
    {
        while (true)
        {
            if (!IsAutoFocusRunning)
            {
                try
                {
                    _cameraService.Capture(out var mat);
                    using (var image = mat)
                    {
                        FocusScore = _imageProcessing.CalculateFocusScore(image, CropSize);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Error updating focus score");
                }
            }
            await Task.Delay(1000); // 每秒更新一次
        }
    }


    [RelayCommand]
    private void CancelAutoFocus()
    {
        _autoFocusCts?.Cancel();
    }


    #endregion


    #region zaber 电动台

    //public ZaberMotorService zaberMotor;

    //public SerialPortModel _sp;

    [RelayCommand]
    void Connect()
    {
        _motor.InitMotor(out string ConnectionStatus);

        Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                (double x, double y, double z) = _motor.ReadPosition();
                CurrentZ = z;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }

        });
    }


    [RelayCommand]
    private void MoveToPosition()
    {
        _motor.SetPosition(CurrentX, CurrentY, CurrentZ);
        UpdatePosition();
    }
    [ObservableProperty] private double _zInterval = 1;

    [RelayCommand]
    void MoveZ(string symbol)
    {
        Task.Run(() =>
        {
            var value = ZInterval * (symbol == "1" ? 1 : -1);

            _motor.MoveRelative(0, 0, value);

            UpdatePosition();
        });
    }


    [RelayCommand]
    private async Task StartAutoFocus()
    {
        if (IsAutoFocusRunning) return;

        try
        {
            IsAutoFocusRunning = true;
            _autoFocusCts = new CancellationTokenSource();

            // 简单扫描对焦
            // double bestZ = await _autoFocusService.PerformAutoFocusAsync(
            //     startZ: _motor.Z - 5,
            //     endZ: _motor.Z + 5,
            //     stepSize: 0.2,
            //     cropSize: CropSize,
            //     cancellationToken: _autoFocusCts.Token);

            // 智能对焦
            double bestZ = await _autoFocusService.SmartAutoFocusAsync(
                initialStep: 1.0,
                minStep: 0.05,
                cropSize: CropSize,
                cancellationToken: _autoFocusCts.Token);

            // 更新位置显示
            UpdatePosition();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Auto focus was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Auto focus failed");
            MessageBox.Show($"Auto focus failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsAutoFocusRunning = false;
            _autoFocusCts?.Dispose();
            _autoFocusCts = null;
        }
    }


    private void InitializeMotor()
    {
        try
        {
            _motor.InitMotor(out string connectionState);
            ConnectionStatus = connectionState;
            UpdateMotorPosition();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error initializing motor: {ex.Message}";
        }
    }

    private void UpdateMotorPosition()
    {
        try
        {
            (double x, double y, double z) = _motor.ReadPosition();
            CurrentX = x;
            CurrentY = y;
            CurrentZ = z;

        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error reading motor position: {ex.Message}";
        }
    }

    private void UpdatePosition()
    {
        (double x, double y, double z) = _motor.ReadPosition();
        CurrentX = x;
        CurrentY = y;
        CurrentZ = z;
    }

    #endregion


    public void Dispose()
    {
        _motor.Stop();

        _videoProcessing.FrameProcessed -= OnFrameProcessed;
        _videoProcessing.FocusScoreUpdated -= OnFocusScoreUpdated;
        _videoProcessing.Dispose();
    }

    ~ShellViewModel()
    {
        // Cleanup resources if necessary
        //_motor.Stop();
    }

}