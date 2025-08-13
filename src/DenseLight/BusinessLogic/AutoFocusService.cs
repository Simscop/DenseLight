using DenseLight.Services;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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

        List<(double z, double focusScore)> _focusScores = new List<(double, double)>();

        public async Task<(double bestZ, List<(double z, double score)> focusScores)> PerformAutoFocusAsync(double startZ,
            double endZ, double stepSize, double cropSize = 0.8, CancellationToken cancellationToken = default)
        {
            if (stepSize == 0) MessageBox.Show("Step size cannot be zero.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            if (_focusScores.Count != 0) { _focusScores = new List<(double, double)>(); }

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

            await Task.Delay(500, cancellationToken);

            Mat snap = new Mat();

            for (int i = 0; i <= steps; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();  // 抛异常以统一处理

                if (i == 0) { continue; }
                // 计算当前Z（线性插值）
                double z = startZ + i * effectiveStep;

                // 移动电机
                await _motor.SetPositionAsync(X, Y, z);

                // 等待稳定
                await Task.Delay(500, cancellationToken);

                _camera.StopCapture(); // 停止当前捕获，确保不会干扰
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

                        // Store the focus score for this position
                        _focusScores.Add((z, focusScore));
                    }
                }
                else
                {
                    _logger.LogError($"Failed to capture image at Z = {z}");
                }

            }

            // Move back to the best position found
            await _motor.SetPositionAsync(X, Y, bestZ);
            // 等待稳定
            await Task.Delay(500, cancellationToken);

            _logger.LogInformation($"Auto-focus completed. Best focus score: {bestFocusScore:F3} at Z = {bestZ}");

            return (bestZ, _focusScores);
        }



        // --- 辅助函数 ---
        public (double topZ, double bottomZ) FindSurfacePeaks(List<(double z, double score)> scores)
        {
            if (scores.Count < 3)
            {
                _logger.LogWarning("Not enough data points for peak detection. Using global best.");
                return (scores[0].z, scores[^1].z);
            }

            // 1. 平滑数据 (移动平均)
            var smoothed = SmoothData(scores.Select(s => s.score).ToList(), windowSize: 3);

            // 2. 计算一阶导数 (中心差分)
            List<double> derivatives = new List<double>();
            for (int i = 1; i < smoothed.Count - 1; i++)
            {
                double diff = (smoothed[i + 1] - smoothed[i - 1]) / (scores[i + 1].z - scores[i - 1].z);
                derivatives.Add(diff);
            }

            // 3. 寻找过零点 (从正变负)
            List<int> peakIndices = new List<int>();
            for (int i = 0; i < derivatives.Count - 1; i++)
            {
                if (derivatives[i] > 0 && derivatives[i + 1] < 0)
                {
                    peakIndices.Add(i + 1); // 对应原始数据的索引
                }
            }

            // 4. 获取两个最高峰
            if (peakIndices.Count < 2)
            {
                _logger.LogWarning($"Only found {peakIndices.Count} peaks. Using fallback strategy.");

                // 回退策略：选择全局最高分和次高分
                var ordered = scores.OrderByDescending(s => s.score).Take(2).ToList();
                return ordered.Count == 2 ?
                    (ordered[0].z, ordered[1].z) :
                    (scores[0].z, scores[^1].z);
            }

            // 按分数排序峰值
            var peakScores = peakIndices
                .Select(idx => (idx, score: scores[idx].score))
                .OrderByDescending(p => p.score)
                .Take(2)
                .ToList();

            double z1 = scores[peakScores[0].idx].z;
            double z2 = scores[peakScores[1].idx].z;

            // 确保上表面Z值较小
            return z1 < z2 ? (z1, z2) : (z2, z1);
        }

        private List<double> SmoothData(List<double> data, int windowSize)
        {
            List<double> smoothed = new List<double>();
            int halfWindow = windowSize / 2;

            for (int i = 0; i < data.Count; i++)
            {
                int start = Math.Max(0, i - halfWindow);
                int end = Math.Min(data.Count - 1, i + halfWindow);

                double sum = 0;
                for (int j = start; j <= end; j++)
                {
                    sum += data[j];
                }

                smoothed.Add(sum / (end - start + 1));
            }

            return smoothed;
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
