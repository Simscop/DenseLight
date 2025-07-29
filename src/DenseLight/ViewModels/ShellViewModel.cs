using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DenseLight.BusinessLogic;
using DenseLight.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DenseLight.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly IMotor _motor;
    private readonly ICameraService _cameraService;
    private readonly AutoFocusService _autoFocusService;
    private readonly ILoggerService _logger;
    private readonly IImageProcessingService _imageProcessingService;

    [ObservableProperty] private double _currentX;
    [ObservableProperty] private double _currentY;
    [ObservableProperty] private double _currentZ;

    [ObservableProperty] private string _connectionStatus = "Disconnected";
    [ObservableProperty] private string _errorMessage = string.Empty;

    [ObservableProperty]
    private double _focusScore;

    [ObservableProperty]
    private double _cropSize = 0.8;

    [ObservableProperty]
    private bool _isAutoFocusRunning;

   
    private readonly IImageProcessingService _imageProcessing;
    private CancellationTokenSource _autoFocusCts;

    public ShellViewModel(IMotor motor, AutoFocusService autoFocusService, ILoggerService logger,
            IImageProcessingService imageProcessing) 
    {
        _motor = motor;
        _autoFocusService = autoFocusService;
        _logger = logger;
        _imageProcessingService = imageProcessing;

        InitializeMotor();
        // 启动定期更新对焦分数
        StartFocusScoreUpdate();
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

    [RelayCommand]
    private void CancelAutoFocus()
    {
        _autoFocusCts?.Cancel();
    }

    [RelayCommand]
    private void MoveToPosition(double x, double y, double z)
    {
        _motor.SetPosition(x, y, z);
        UpdatePosition();
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
            _motor.ReadPosition();

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

    ~ShellViewModel()
    {
        // Cleanup resources if necessary
        _motor.Stop();
    }

}