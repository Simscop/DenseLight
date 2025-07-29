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
        private readonly ILoggerService _logger;
        private readonly IImageProcessingService _imageProcessingService;

        public AutoFocusService(IMotor motor, 
            ICameraService camera, 
            ILoggerService logger, 
            IImageProcessingService imageProcessing)
        {
            _motor = motor;
            _camera = camera;  
            _logger = logger;
            _imageProcessingService = imageProcessing;
        }

        public async Task<double> PerformAutoFocusAsync(double startZ, 
            double endZ, double stepSize, double cropSize = 0.8, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"Starting auto-focus from {startZ} to {endZ} with step {stepSize}");

            (double X, double Y, double Z) = _motor.ReadPosition();
            double currentZ = Z;
            double bestZ = currentZ;
           
            int steps = (int)Math.Ceiling((endZ - startZ) / stepSize);

            // 方向判断
            bool movingForward = stepSize > 0;
            double sign = movingForward ? 1.0 : -1.0;

            _logger.LogInformation($"Auto-focus direction: {(movingForward ? "forward" : "backward")}, total steps: {steps}");

            // Move to the starting position
            _motor.SetPosition(X, Y, startZ);

            double bestFocusScore = 0.0;

            for (int i = 0; i <= steps; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Auto-focus operation cancelled.");
                    return bestFocusScore;
                }
                // 当前Z位置
                double z = startZ + i * stepSize * sign;

                if ((movingForward && z > endZ) || (!movingForward && z < endZ))
                {
                    z = endZ;
                }

                // 移动电机
                _motor.SetPosition(X, Y, z);

                // 等待稳定
                await Task.Delay(50, cancellationToken); // 等待100毫秒，确保电机移动稳定

                // Capture an image
                var isCapture = _camera.Capture(out var image);
                
                if (isCapture)
                {
                    using (var img = image)
                    {
                        if (img == null || img.Empty())
                        {
                            _logger.LogError($"Failed to capture image at Z = {z}");
                            continue;
                        }
                        // Calculate focus score
                        double focusScore = _imageProcessingService.CalculateFocusScore(image, cropSize);
                        _logger.LogInformation($"Focus score at Z={z}: {focusScore:F3}");
                        // Check if this is the best focus score
                        if (focusScore > bestFocusScore)
                        {
                            bestFocusScore = focusScore;
                            bestZ = z;
                            _logger.LogInformation($"New best focus score: {bestFocusScore:F3} at Z = {bestZ}");
                        }
                    }


                }             
                


            }

            // Move back to the best position found
            _motor.SetPosition(X, Y, bestZ);
            _logger.LogInformation($"Auto-focus completed. Best focus score: {bestFocusScore:F3} at Z = {bestZ}");

            return bestZ;
        }

        // 智能对焦算法（使用爬山法优化）
        public async Task<double> SmartAutoFocusAsync(
            double initialStep = 1.0,
            double minStep = 0.05,
            double cropSize = 0.8,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting smart auto focus");

                (double x, double y, double z) = _motor.ReadPosition();
                double currentZ = z;
                double currentScore = await GetCurrentFocusScore(cropSize);
                double step = initialStep;
                int direction = 1; // 1 for positive, -1 for negative
                double bestZ = currentZ;
                double bestScore = currentScore;

                _logger.LogInformation($"Initial position: Z={currentZ:F3}, Score={currentScore:F3}");

                while (step >= minStep)
                {
                    // 尝试正向移动
                    double newZ = currentZ + step * direction;
                    _motor.SetPosition(x, y, newZ);
                    await Task.Delay(50, cancellationToken);

                    double newScore = await GetCurrentFocusScore(cropSize);
                    _logger.LogDebug($"Trying Z={newZ:F3}, Score={newScore:F3}, Step={step:F3}, Dir={direction}");

                    if (newScore > currentScore)
                    {
                        // 找到更好的位置
                        currentZ = newZ;
                        currentScore = newScore;

                        if (newScore > bestScore)
                        {
                            bestZ = newZ;
                            bestScore = newScore;
                            _logger.LogInformation($"New best position: Z={bestZ:F3}, Score={bestScore:F3}");
                        }
                    }
                    else
                    {
                        // 反方向尝试
                        direction *= -1;

                        // 如果两个方向都不好，减小步长
                        if (direction == -1)
                        {
                            step /= 2;
                            _logger.LogInformation($"Reducing step size to {step:F3}");
                        }
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Smart auto focus cancelled");
                        break;
                    }
                }

                // 移动到最佳位置
                _motor.SetPosition(x, y, bestZ);
                _logger.LogInformation($"Smart auto focus completed. Best Z: {bestZ:F3}, Score: {bestScore:F3}");

                return bestZ;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Smart auto focus failed");
                throw;
            }
        }

        private async Task<double> GetCurrentFocusScore(double cropSize)
        {
            _camera.Capture(out var image);
            using (var img = image)
            {
                return _imageProcessingService.CalculateFocusScore(image, cropSize);
            }
        }

    }
}
