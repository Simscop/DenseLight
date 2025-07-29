using System.Transactions;

namespace DenseLight.Services
{
    public interface IMotor
    {
        /* PC to Controller */

        public void InitMotor(out string connectionState);

        public (double X, double Y, double Z) ReadPosition(); // x y z

        public void SetPosition(double x, double y, double z); // x y z

        public void SetAcceleration();

        public void SetDeceleration();

        public void SetSpeed(double velocity);

        public bool Stop();

        public bool MoveRelative(double x, double y, double z); // get error command

        public bool MoveAbsolute(double x, double y, double z);

        public bool SetContinuousMove();  // get error command

        public MotionState GetMotionState(); // x y z

        public void ResetToZero();

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

    public enum MotionState
    {
        Busy,
        Idle,
        Home
    }
}
