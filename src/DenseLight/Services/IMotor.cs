namespace DenseLight.Services
{
    public interface IMotor
    {
        /* PC to Controller */
        public double X { get; }

        public double Y { get; }

        public double Z { get; }

        public void InitMotor(out string connectionState);

        public void ReadPosition(); // x y z

        public void SetPosition(double x, double y, double z); // x y z

        public void SetAcceleration();

        public void SetDeceleration();

        public void SetSpeed();

        public void Stop();

        public bool SetOffset(); // get error command

        public bool SetContinuousMove();  // get error command

        public bool GetMotionState(); // x y z

        public bool ResetToZero();

        /* Controller to PC */

        public bool HasMovedIntoPosition(); // after motor stop

        public bool GetErrorCommand(); // receive command in motor movement

        public bool ResetCompleted(); // after motor reset

    }

    public class MotorStatus
    {
        public bool IsMoving { get; set; } = false;
        public bool IsStopped { get; set; } = true;
        public bool IsReset { get; set; } = false;
        public bool HasError { get; set; } = false;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
