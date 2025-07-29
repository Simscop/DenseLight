using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DenseLight.BusinessLogic;
using DenseLight.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DenseLight.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly IMotor _motor;
    private readonly ICameraService _cameraService;
    private readonly AutoFocusService _autoFocusService;

    [ObservableProperty] private double _currentX;
    [ObservableProperty] private double _currentY;
    [ObservableProperty] private double _currentZ;

    [ObservableProperty] private string _connectionStatus = "Disconnected";
    [ObservableProperty] private string _errorMessage = string.Empty;

    public ShellViewModel(IMotor motor, AutoFocusService autoFocusService)
    {
        _motor = motor;
        _autoFocusService = autoFocusService;
        InitializeMotor();

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

    [RelayCommand]
    private void AutoFocus() => _autoFocusService.PerformAutoFocus(100, 10, 20);

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

    ~ShellViewModel()
    {
        // Cleanup resources if necessary
        _motor.Stop();
    }

}