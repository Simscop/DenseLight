using DenseLight.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DenseLight.BusinessLogic
{
    public class MotionControlService
    {
        private readonly IMotor _motor;

        public MotionControlService(IMotor motor)
        {
            _motor = motor;
        }

        public void InitializeMotor(out string connectionState)
        {
            _motor.InitMotor(out connectionState);
        }

        public void MoveToPosition(double x, double y, double z)
        {
            // Implement logic to move the motor to the specified position
            // This is a placeholder for actual movement logic
            Console.WriteLine($"Moving motor to position X: {x}, Y: {y}, Z: {z}");
            _motor.SetPosition(x, y, z); // Assuming SetPosition is implemented in IMotor
        }

        public void StopMovement()
        {
            // Implement logic to stop the motor movement
            Console.WriteLine("Stopping motor movement.");
            _motor.Stop(); // Assuming Stop is implemented in IMotor
        }
    }
}
