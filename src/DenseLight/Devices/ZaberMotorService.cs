using DenseLight.Services;

namespace DenseLight.Devices
{
    public class ZaberMotorService : IMotor
    {
        // TODO 所有硬件使用异步调用，避免阻塞UI线程 添加位置边界检查和错误处理 使用CancellationToken支持取消操作


        public double X => throw new NotImplementedException();

        public double Y => throw new NotImplementedException();

        public double Z => throw new NotImplementedException();

        public bool GetErrorCommand()
        {
            throw new NotImplementedException();
        }

        public bool GetMotionState()
        {
            throw new NotImplementedException();
        }

        public bool HasMovedIntoPosition()
        {
            throw new NotImplementedException();
        }

        public void InitMotor(out string connectionState)
        {
            throw new NotImplementedException();
        }

        public void ReadPosition()
        {
            throw new NotImplementedException();
        }

        public bool ResetCompleted()
        {
            throw new NotImplementedException();
        }

        public bool ResetToZero()
        {
            throw new NotImplementedException();
        }

        public void SetAcceleration()
        {
            throw new NotImplementedException();
        }

        public bool SetContinuousMove()
        {
            throw new NotImplementedException();
        }

        public void SetDeceleration()
        {
            throw new NotImplementedException();
        }

        public bool SetOffset()
        {
            throw new NotImplementedException();
        }

        public void SetPosition()
        {
            throw new NotImplementedException();
        }

        public void SetPosition(double x, double y, double z)
        {
            throw new NotImplementedException();
        }

        public void SetSpeed()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
}
