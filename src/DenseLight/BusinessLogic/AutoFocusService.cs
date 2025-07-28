using DenseLight.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DenseLight.BusinessLogic
{
    public class AutoFocusService
    {
        private readonly ICameraService _camera;
        private readonly IMotor _motor;

        public AutoFocusService(IMotor motor, ICameraService camera)
        {
            _motor = motor;
            _camera = camera;                
        }

        public void PerformAutoFocus(int exposureTimeMs, int stepSize, int maxSteps)
        {
            _camera.InitializeCamera();
            _camera.SetExposureTime(exposureTimeMs);
            _camera.StartContinuousCapture();

            double step = 0.1;
            double bestZ = 0;
            double bestFocus = 0;

            var CurrentX = _motor.X;
            var CurrentY = _motor.Y;

            double minZ = _motor.Z - stepSize * maxSteps / 2.0;
            double maxZ = _motor.Z + stepSize * maxSteps / 2.0;

            for (var z = minZ; z < maxZ; z += step)
            {
                _motor.SetPosition(CurrentX, CurrentY, z);
                Thread.Sleep(100); // Wait for motor to stabilize

                // Capture image and analyze focus quality here
                var image = _camera.CaptureImage();
                // Analyze the image for focus quality (not implemented here)

                //double focusQuality = _camera.AnalyzeFocusQuality(image);

                //if (focusQuality > bestFocus)
                //{
                //    bestFocus = focusQuality;
                //    bestZ = z;
                //}

                // Check if the motor has moved into position
                if (!_motor.HasMovedIntoPosition())
                {
                    throw new Exception("Motor did not reach the expected position.");
                }
            }

            _motor.SetPosition(CurrentX, CurrentY, bestZ);

            _camera.StopContinuousCapture();
            _camera.DisconnectCamera();
        }   
    }
}
