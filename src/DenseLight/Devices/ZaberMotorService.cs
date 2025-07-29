using DenseLight.Services;
using Zaber.Motion.Ascii;

namespace DenseLight.Devices
{
    public class ZaberMotorService : IMotor
    {
        // TODO 所有硬件使用异步调用，避免阻塞UI线程 添加位置边界检查和错误处理 使用CancellationToken支持取消操作
        // var connection = Connection.OpenSerialPort("COM3");
        // var device = connection.GetDevice(1);
        // var axis = device.GetAxis(1);
        // var axisGroup = new AxisGroup(new Axis[] { axis });
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        private AxisGroup _axisGroup;

        private Axis? _xAxis;

        private Axis? _yAxis;

        private Axis? _zAxis;

        private Connection Connection { get; set; }

        public string _port = "COM3";

        private Device[]? deviceList;

        public enum MotionState
        {
            Busy,
            Idle,
            Home
        }

        public Zaber.Motion.Units Units { get; set; } = Zaber.Motion.Units.Length_Nanometres;

        public Zaber.Motion.Units vUnits { get; set; } = Zaber.Motion.Units.Velocity_NanometresPerSecond;

        public bool GetErrorCommand()
        {
            return false;
        }

        Services.MotionState IMotor.GetMotionState()
        {
            bool isBusy = _axisGroup.IsBusy();
            if (isBusy) return Services.MotionState.Busy;
            bool isHomed = _axisGroup.IsHomed();
            return isHomed ? Services.MotionState.Home : Services.MotionState.Idle;

        }

        public bool HasMovedIntoPosition()
        {
            bool isBusy = _axisGroup.IsBusy();
            if (isBusy)
            {
                _axisGroup.WaitUntilIdle();
                return false;
            }
            else
            {
                return true;
            }
        }

        public void InitMotor(out string connectionState)
        {
            if (Connection == null)
            {
                Connection = Connection.OpenSerialPort(_port);
                Connection.EnableAlerts();
                deviceList = Connection.DetectDevices(true);
            }

            Console.WriteLine($"Found {deviceList?.Length} devices.");

            _zAxis = deviceList?.FirstOrDefault()?.GetAxis(1); // Assuming the first device has an axis 1
            _yAxis = deviceList?.FirstOrDefault()?.GetAxis(2); // Assuming the first device has an axis 2
            _xAxis = deviceList?.FirstOrDefault()?.GetAxis(3); // Assuming the first device has an axis 3

            connectionState = "Connected to Zaber Motor Service";

        }

        public void ReadPosition()
        {
            // TODO 支持异步调用，按顺序请求坐标，在轴停止后调用 axisGroup.GetPosition(params unit)

            X = _xAxis?.GetPosition(Units) ?? double.NaN;
            Y = _yAxis?.GetPosition(Units) ?? double.NaN;
            Z = _zAxis?.GetPosition(Units) ?? double.NaN;

        }

        public bool ResetCompleted()
        {
            return true;
        }

        public void ResetToZero()
        {
            if (!_zAxis.IsHomed())
            {
                _zAxis.Home(true);
            }
        }

        public void SetAcceleration()
        {
            return;
        }

        public bool SetContinuousMove()
        {
            return false;
        }

        public void SetDeceleration()
        {
            return;
        }

        public void SetPosition(double x, double y, double z)
        {
            return;
        }

        public void SetSpeed(double velocity)
        {
            if (_zAxis != null)
            {
                _zAxis.MoveVelocity(velocity, vUnits);
            }
        }

        public bool Stop()
        {
            if (_zAxis != null)
            {
                _zAxis.Stop();
            }
            return true;
        }

        public bool MoveRelative(double x, double y, double z)
        {
            try
            {
                _xAxis?.MoveRelativeAsync(x, Units, true);
                _yAxis?.MoveRelativeAsync(y, Units, true);
                _zAxis?.MoveRelativeAsync(z, Units, true);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public bool MoveAbsolute(double x, double y, double z)
        {
            try
            {
                _xAxis?.MoveAbsoluteAsync(x, Units, true);
                _yAxis?.MoveAbsoluteAsync(y, Units, true);
                _zAxis?.MoveAbsoluteAsync(z, Units, true);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

        }
    }
}
