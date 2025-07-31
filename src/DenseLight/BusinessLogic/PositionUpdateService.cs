using DenseLight.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DenseLight.BusinessLogic
{
    // 1. 将 _updateTimer 字段声明为可为 null（加上 ?），以消除 CS8618。
    // 2. 将 PropertyChanged 事件声明为可为 null（加上 ?），以消除 CS8618。

    public class PositionUpdateService : IDisposable
    {
        private readonly IMotor? _motor;
        private Timer? _updateTimer;

        public event PropertyChangedEventHandler? PropertyChanged;

        public double X { get; private set; }
        public double Y { get; private set; }
        public double Z { get; private set; }

        public PositionUpdateService(IMotor motor)
        {
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
            Initialize();
        }

        private void Initialize()
        {
            // 初始化位置
            UpdatePosition();

            // 创建定时器
            _updateTimer = new Timer(UpdatePositionCallback, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        }

        private void UpdatePositionCallback(object? state)
        {
            try
            {
                UpdatePosition();
            }
            catch (Exception ex)
            {
                // Handle exceptions, possibly log them or notify the user
                Console.WriteLine($"Error updating position: {ex.Message}");
            }
        }

        private void UpdatePosition()
        {
            try
            {
                var position = _motor.ReadPosition();
                X = position.X;
                Y = position.Y;
                Z = position.Z;

                // Notify property changes
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Y)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Z)));
            }
            catch (Exception ex)
            {
                // Handle exceptions, possibly log them or notify the user
                Console.WriteLine($"Error updating position: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _updateTimer?.Dispose();
            _updateTimer = null;
        }
    }
}
