using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DenseLight.BusinessLogic;
using DenseLight.Devices;
using DenseLight.Models;
using DenseLight.Services;
using Lift.UI.Tools;
using OpenCvSharp;
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

        [ObservableProperty] private int _currentStep; // 当前步数

        [ObservableProperty] private double _progress = 0; // 进度 0-100%

        private double cropSize = 0.8;

        private Mat _snapShot;
        private readonly object _snapShotLock = new object();  // 新增：锁保护 _snapShot 访问
        [ObservableProperty]
        private bool isImageAvailable;  // 新增：是否已有有效图像，绑定到 UI

        private CancellationTokenSource? _cts;  // 用于管理取消的源

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

            IsConnected = true;

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
        private async Task ZStackScan(CancellationToken token)
        {
            if (IsBusy) return;
            IsBusy = true;
            CurrentStep = 0;

            // 初步读取
            (double x, double y, double z) = await _motor.ReadPositionAsync();

            // 设置初始位置到ZTop
            await _motor.SetPositionAsync(x, y, z);

            // 计算步数（注意：ZBottom > ZTop 假设向下扫描）
            int steps = (int)Math.Ceiling(Math.Abs(ZBottom - ZTop) / ZStep);  // 使用 Abs 防负值

            for (int i = 0; i <= steps; i++)
            {
                token.ThrowIfCancellationRequested();  // 支持取消

                await _motor.MoveRelativeAsync(0, 0, ZStep);

                await Task.Delay(50);

                UpdatePositionFromService();

                CurrentStep = i;
                Progress = (double)(i + 1) / (steps + 1) * 100;

                // 小延迟防止过快循环
                await Task.Delay(100, token);
            }
            IsBusy = false;
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
        private void CancelScan()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();  // 触发取消信号                
            }
        }

        [RelayCommand]
        void MoveZ(string symbol)
        {
            var value = ZStep * (symbol == "1" ? 1 : -1);
            _motor.MoveRelative(0, 0, value);

            (double x, double y, double z) = _motor.ReadPosition();
            Z = double.IsNaN(z) ? 0 : z; // 刷新坐标

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
        private async Task Focus()
        {
            if (IsBusy || !IsImageAvailable)
            {
                // 修复：WPF 应用没有 MainPage，使用 MessageBox 显示提示
                MessageBox.Show("无可用图像，请等待相机捕获或重试。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            IsBusy = true;

            try
            {
                _cts = new CancellationTokenSource();
                Mat? localSnap;

                lock (_snapShotLock)
                {
                    localSnap = _snapShot.Clone(); // 克隆以防原图修改
                }

                double bestZ = await _autoFocusService.PerformAutoFocusAsync(localSnap, ZTop, ZBottom, ZStep, cropSize, _cts.Token);

                //double bestZ = await _autoFocusService.SmartAutoFocusAsync(localSnap, 10, 1, 0.8, 50, 2, _cts.Token);

                Z = bestZ;
            }
            catch (Exception e) { }
            finally
            {
                IsBusy = false;
                _cts?.Dispose();
                _cts = null;
            }
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

            WeakReferenceMessenger.Default.Register<DisplayFrame, string>(this, "Display", (sender, message) =>
            {
                Application.Current.Dispatcher.Invoke(() =>  // 异步更新，避免阻塞线程
                {
                    lock (_snapShotLock) // 保护写入
                    {
                        if (message?.Image != null && !message.Image.Empty())
                        {
                            _snapShot?.Dispose();
                            _snapShot = message.Image.Clone();
                            IsImageAvailable = true;
                        }
                        else
                        {
                            _snapShot?.Dispose();
                            _snapShot = null;
                            IsImageAvailable = _snapShot != null;
                            return;
                        }
                    }
                });
            });
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
