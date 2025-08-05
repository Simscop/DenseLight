using DenseLight.Services;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
            if (stepSize == 0) return 0.0;

            _logger.LogInformation($"Starting auto-focus from {startZ} to {endZ} with step {stepSize}");

            var (X, Y, Z) = await _motor.ReadPositionAsync(); // 假设异步
            double currentZ = Z;
            double bestZ = startZ; // 初始化为起始z
            double bestFocusScore = double.MinValue;

            // 计算绝对距离和步数，确保 steps 正
            double distance = Math.Abs(endZ - startZ);
            int steps = (int)Math.Round(distance / Math.Abs(stepSize));  // 用 Abs 防负

            // 方向：正向 (startZ -> endZ) 根据 endZ > startZ 和 stepSize 符号
            double effectiveStep = Math.Sign(endZ - startZ) * Math.Abs(stepSize);  // 统一步长方向         

            _logger.LogInformation($"Auto-focus direction: {(effectiveStep > 0 ? "forward" : "backward")}, total steps: {steps}");

            // Move to the starting position
            await _motor.SetPositionAsync(X, Y, startZ);

            await Task.Delay(200, cancellationToken);

            Mat snap = new Mat();

            for (int i = 0; i <= steps; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();  // 抛异常以统一处理

                if (i == 0) { continue; }
                // 计算当前Z（线性插值）
                double z = startZ + i * effectiveStep;

                if ((effectiveStep > 0 && z > endZ) || (effectiveStep < 0 && z < endZ))
                {
                    currentZ = endZ;
                }

                // 移动电机
                await _motor.SetPositionAsync(X, Y, z);

                // 等待稳定
                await Task.Delay(100, cancellationToken);

                _camera.Capture(out snap);

                if (snap != null && !snap.Empty())
                {
                    using (snap)
                    {
                        double focusScore = _imageProcessingService.CalculateFocusScore(snap, cropSize);
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
                else
                {
                    _logger.LogError($"Failed to capture image at Z = {z}");
                }

                // Move back to the best position found
                await _motor.SetPositionAsync(X, Y, bestZ);
            }

            // Move back to the best position found
            await _motor.SetPositionAsync(X, Y, bestZ);
            _logger.LogInformation($"Auto-focus completed. Best focus score: {bestFocusScore:F3} at Z = {bestZ}");

            return bestZ;
        }

        // 将 SmartAutoFocusAsync 和 GetCurrentFocusScoreAsync 方法中的 CancellationTokenSource 参数类型改为 CancellationToken
        public async Task<double> SmartAutoFocusAsync(
            double initialStep = 1.0,
            double minStep = 0.05,
            double cropSize = 0.8,
            int maxIterations = 50,
            double stepReductionFactor = 2.0,
            CancellationToken cancellationToken = default) // 修正参数类型
        {

            _logger.LogInformation("Starting smart auto focus");
            Mat snap = new Mat();
            _camera.Capture(out snap);

            var (x, y, z) = await _motor.ReadPositionAsync();
            double currentZ = z;
            double bestZ = currentZ;
            double bestScore = await GetCurrentFocusScoreAsync(snap, cropSize, cancellationToken);
            double currentScore = bestScore;
            double step = initialStep;
            int direction = 1;
            int iterations = 0;
            int failedAttempts = 0;

            _logger.LogInformation($"Initial position: Z={currentZ:F3}, Score={currentScore:F3}");

            while (step >= minStep && iterations < maxIterations)
            {
                cancellationToken.ThrowIfCancellationRequested(); // 修正为 CancellationToken
                iterations++;
                double newZ = currentZ + step * direction;

                await _motor.SetPositionAsync(x, y, newZ);

                await Task.Delay(100, cancellationToken);

                _camera.Capture(out snap);

                double newScore = await GetCurrentFocusScoreAsync(snap, cropSize, cancellationToken);
                _logger.LogDebug($"Trying Z={newZ:F3}, Score={newScore:F3}, Step={step:F3}, Dir={direction}");

                if (newScore > currentScore)
                {
                    currentZ = newZ;
                    currentScore = newScore;
                    failedAttempts = 0;

                    if (newScore > bestScore)
                    {
                        bestZ = newZ;
                        bestScore = newScore;
                        _logger.LogInformation($"New best position: Z={bestZ:F3}, Score={bestScore:F3}");
                    }
                }
                else
                {
                    direction *= -1;
                    failedAttempts++;

                    if (failedAttempts >= 2)
                    {
                        step /= stepReductionFactor;
                        failedAttempts = 0;
                        _logger.LogInformation($"Reducing step size to {step:F3}");
                    }
                }
            }

            await _motor.SetPositionAsync(x, y, bestZ);
            _logger.LogInformation($"Smart auto focus completed. Best Z: {bestZ:F3}, Score: {bestScore:F3}, . Iterations: {iterations}");

            return bestZ;
        }

        // 修正 GetCurrentFocusScoreAsync 的参数类型
        private async Task<double> GetCurrentFocusScoreAsync(Mat image, double cropSize, CancellationToken cancellationToken)
        {
            if (image == null || image.Empty())
            {
                _logger.LogWarning("Failed to capture image for focus score");
                return 0.0;
            }

            var img = image;
            using (img)
            {
                return _imageProcessingService.CalculateFocusScore(img, cropSize);
            }
        }

    }
}
