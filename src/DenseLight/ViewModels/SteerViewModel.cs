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
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DenseLight.ViewModels
{
    public partial class SteerViewModel : ObservableObject
    {
        private readonly PositionUpdateService _positionUpdateService;
        private readonly AutoFocusService _autoFocusService;
        private readonly DispatcherTimer _timer;

        private readonly IMotor _motor;

        private ZaberMotorService? _zaberDevice;

        [ObservableProperty]
        private string _selectedCom = string.Empty;

        [ObservableProperty]
        public ObservableCollection<string> _coms;

        [ObservableProperty] private double _x;

        [ObservableProperty] public double _y;

        [ObservableProperty] private double _z;

        [ObservableProperty] private double _zStep = 1;

        [ObservableProperty] private double _zTop = 22999900;

        [ObservableProperty] private double _zBottom = 23000000;

        [ObservableProperty] public bool _isConnected = false;

        [ObservableProperty] private string _connectionStatus = "连接已断开";

        [ObservableProperty] private bool _isBusy = false;

        private double cropSize = 0.8;

        partial void OnZChanged(double value)
        {
            const double max = 23010000; // nm
            const double min = 0;
            if (value < min || value > max)
            {
                Z = Math.Clamp(value, min, max);
            }
        }

        partial void OnZBottomChanged(double value)
        {
            const double max = 23010000; // nm
            const double min = 0;
            if (value < min || value > max)
            {
                ZBottom = Math.Clamp(value, min, max);
            }
        }

        partial void OnZTopChanged(double value)
        {
            const double max = 23010000; // nm
            const double min = 0;
            if (value < min || value > max)
            {
                ZTop = Math.Clamp(value, min, max);
            }
        }


        [RelayCommand]
        void Connect()
        {
            ConnectionStatus = "正在连接中...";

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

        [RelayCommand]
        void ZStackScan()
        {
            (double x, double y, double z) = _motor.ReadPosition();

            X = x; Y = y; Z = z;

            _motor.SetPosition(X, Y, ZTop);

            (X, Y, Z) = _motor.ReadPosition();

            int steps = (int)Math.Ceiling((int)(ZBottom - ZTop) / ZStep);

            for (int i = 0; i <= steps; i++)
            {
                _motor.MoveRelative(0, 0, ZStep);

                (X, Y, Z) = _motor.ReadPosition();
            }

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
            var value = ZStep * (symbol == "1" ? 1 : -1);
            _motor.MoveRelative(0, 0, value);

            (double x, double y, double z) = _motor.ReadPosition();
            Z = double.IsNaN(z) ? 0 : z;

            //Task.Run(() =>
            //{
            //    var value = ZStep * (symbol == "1" ? 1 : -1);
            //    _motor.MoveRelative(0, 0, value);
            //});

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
        private async Task Focus(CancellationToken token)
        {
            if (IsBusy) return;

            IsBusy = true;

            try
            {
                double bestZ = await _autoFocusService.PerformAutoFocusAsync(ZTop, ZBottom, ZStep, cropSize, token);
                _motor.MoveAbsolute(X, Y, Z);
                Z = bestZ;
            }
            catch (Exception e) { }
            finally { IsBusy = false; }

        }

        public SteerViewModel(PositionUpdateService positionUpdateService, IMotor motor, AutoFocusService autoFocusService)
        {
            _positionUpdateService = positionUpdateService ?? throw new ArgumentNullException(nameof(positionUpdateService));
            _autoFocusService = autoFocusService;
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));

            // 订阅位置更新事件
            _positionUpdateService.PropertyChanged += OnPositionChanged;

            UpdatePositionFromService(); // read position once 

            Coms = new ObservableCollection<string>(SerialPort.GetPortNames());
        }

        private void UpdatePositionFromService()
        {
            X = _positionUpdateService.X; Y = _positionUpdateService.Y; Z = _positionUpdateService.Z;
        }

        private void OnPositionChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 在UI线程上更新属性
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.PropertyName == nameof(PositionUpdateService.Z))
                {
                    Z = _positionUpdateService.Z;
                }
            });
        }

        [RelayCommand]
        private void RefreshPosition()
        {
            UpdatePositionFromService();
        }

        ~SteerViewModel() { _positionUpdateService.PropertyChanged -= OnPositionChanged; }

        public void Cleanup()
        {
            _positionUpdateService.PropertyChanged -= OnPositionChanged;
        }
    }
}
