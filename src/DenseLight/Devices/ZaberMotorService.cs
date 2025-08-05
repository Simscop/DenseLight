using DenseLight.Services;
using System.IO.Ports;
using Zaber.Motion.Ascii;

namespace DenseLight.Devices
{
    public class SerialPortModel
    {
        /// <summary>
        /// 获取或设置串口的名称。
        /// </summary>
        /// <remarks>
        /// 通常是COM端口的标识符，如 "COM1", "COM2" 等。
        /// </remarks>
        public string Name { get; set; } = "COM9";
    }

    public class ZaberMotorService : IMotor
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        private AxisGroup _axisGroup;

        private Axis? _xAxis;

        private Axis? _yAxis;

        private Axis? _zAxis;

        private Connection Connection { get; set; }

        public string _port = "COM9";

        private Device[]? deviceList;

        public enum MotionState
        {
            Busy,
            Idle,
            Home
        }

        public SerialPortModel spModel { get; set; }

        private readonly SerialPort _sp;

        //public ZaberMotorService(SerialPortModel model)
        //{
        //    spModel = model;
        //    _sp = new SerialPort()
        //    {
        //        PortName = model.Name,
        //    };
        //    //_sp.Open();
        //    //_port = _sp.PortName;
        //}

        public Zaber.Motion.Units Units { get; set; } = Zaber.Motion.Units.Length_Nanometres;

        public Zaber.Motion.Units vUnits { get; set; } = Zaber.Motion.Units.Velocity_NanometresPerSecond;

        /// <summary>
        /// 设置串口名称的公共方法。
        /// </summary>
        /// <param name="portName"></param>
        public void SetPort(string portName)
        {
            _port = portName;
        }

        public bool GetErrorCommand()
        {
            return false;
        }

        Services.MotionState IMotor.GetMotionState()
        {
            bool isBusy = _zAxis.IsBusy();
            if (isBusy) return Services.MotionState.Busy;
            bool isHomed = _zAxis.IsHomed();
            return isHomed ? Services.MotionState.Home : Services.MotionState.Idle;

        }

        public bool HasMovedIntoPosition()
        {
            bool isBusy = _zAxis.IsBusy();
            if (isBusy)
            {
                _zAxis.WaitUntilIdle();
                return false;
            }
            else
            {
                return true;
            }
        }

        public bool InitMotor(out string connectionState)
        {
            connectionState = "";

            if (Connection == null)
            {
                Connection = Connection.OpenSerialPortAsync(_port).Result;
                Connection.EnableAlerts();
                deviceList = Connection.DetectDevices(true);

                if (deviceList == null)
                {
                    connectionState = "Zaber连接失败，请检查连接！";
                    return false;
                }
                else
                {
                    Console.WriteLine($"Found {deviceList?.Length} devices.");

                    _zAxis = deviceList?[3].GetAxis(1); // check device
                    _yAxis = deviceList?[5].GetAxis(1);
                    _xAxis = deviceList?[5].GetAxis(2);

                    connectionState = "Zaber连接成功！";
                    return true;
                }
            }
            else
            {
                connectionState = "Zaber连接失败，请检查连接！";
                return false;
            }

        }

        public (double X, double Y, double Z) ReadPosition()
        {
            X = _xAxis?.GetPosition(Units) ?? double.NaN;
            Y = _yAxis?.GetPosition(Units) ?? double.NaN;
            Z = _zAxis?.GetPosition(Units) ?? double.NaN;
            return (X, Y, Z);
        }

        public bool ResetCompleted()
        {
            return true;
        }

        public void ResetToZero()
        {
            if (!_zAxis.IsHomed())
            {
                _zAxis.Home();
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

        public bool SetPosition(double x, double y, double z) => MoveAbsolute(x, y, z);

        public bool SetOffset(double x, double y, double z) => MoveRelative(x, y, z);

        public void SetSpeed(double velocity)
        {
            if (_zAxis != null)
            {
                _zAxis.MoveVelocity(velocity, vUnits);
            }
        }

        public bool Stop()
        {
            if (_zAxis.IsBusy())
            {
                _zAxis.Stop();
                return true;
            }
            else
            {
                return false;
            }

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
