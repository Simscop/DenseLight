using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DenseLight.BusinessLogic;
using DenseLight.Devices;
using DenseLight.Services;
using Lift.UI.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DenseLight.ViewModels
{
    public partial class SteerViewModel : ObservableObject
    {
        private readonly PositionUpdateService _positionUpdateService;
        private readonly DispatcherTimer _timer;

        private readonly IMotor _motor;

        private ZaberMotorService? _zaberDevice;

        [ObservableProperty]
        private string _selectedCom = string.Empty;

        [ObservableProperty]
        public ObservableCollection<string> _coms;

        [ObservableProperty] private double _z;

        [ObservableProperty] private double _zStep = 1;

        [ObservableProperty] public bool _isConnected = false;

        [ObservableProperty] private string _positionStatus = "位置更新中...";

        [ObservableProperty] private string _connectionStatus = "Disconnected";

        [RelayCommand]
        void Connect()
        {
            Console.WriteLine("连接");

            _motor.InitMotor(out string connectState);

            ConnectionStatus = connectState;

            // 代码块调度到UI线程中执行
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    (double x, double y, double z) = _motor.ReadPosition();
                    Z = double.IsNaN(z) ? 0 : z;
                }
                catch (Exception e)
                {
                    
                }
            });

        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            Task.Run(() =>
            {
                (double x, double y, double z) = _motor.ReadPosition();
                Z = double.IsNaN(z) ? 0 : z;

            });
        }

        [RelayCommand]
        void MoveZ(string symbol)
        {
            Task.Run(() =>
            {
                var value = ZStep * (symbol == "1" ? 1 : -1);
                _motor.MoveRelative(0, 0, value);
            });

        }

        [RelayCommand]
        void Stop()
        {
            _motor.Stop();
        }

        [RelayCommand]
        void Reset()
        {
            _motor.ResetToZero();
        }

        [RelayCommand]
        private async Task Focus()
        {

        }

        partial void OnZChanged(double value)
        {
            const double max = 23000000; // nm
            const double min = 0;
            if (value < min || value > max)
            {
                Z = Math.Clamp(value, min, max);
            }
        }

        public SteerViewModel(PositionUpdateService positionUpdateService, IMotor motor)
        {
            _positionUpdateService = positionUpdateService ?? throw new ArgumentNullException(nameof(positionUpdateService));
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));

            // 订阅位置更新事件
            _positionUpdateService.PropertyChanged += OnPositionChanged;
            //SteerInit();

            _timer = new DispatcherTimer(priority: DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };

            Coms = new ObservableCollection<string>(SerialPort.GetPortNames());
        }

        private void SteerInit()
        {
            IsConnected = _motor.InitMotor(out var connectState);

            if (IsConnected)
            {
                _timer.Tick += Timer_Tick;
                _timer.Start();
                ConnectionStatus = connectState;
            }
        }

        private void OnPositionChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 在UI线程上更新属性
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.PropertyName == nameof(PositionUpdateService.Z))
                {
                    Z = _positionUpdateService.Z;
                    PositionStatus = $"最后更新：{DateTime.Now:HH:ss.fff}";
                }
            });
        }

        public void Cleanup()
        {
            _positionUpdateService.PropertyChanged -= OnPositionChanged;
        }
    }
}
