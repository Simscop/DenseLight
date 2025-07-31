using DenseLight.Services;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace DenseLight.BusinessLogic
{
    public class VideoProcessingService : IDisposable
    {
        private readonly ICameraService _camera;
        private readonly ILoggerService _logger;
        private readonly IImageProcessingService _imageProcessing;
        private CancellationTokenSource _processingCts;
        private Task _processingTask;
        private volatile bool _isProcessing = false;
        private int _targetFps = 30;
        private int _frameCounter = 0;
        private double _frameInterval;
        private DateTime _lastFrameTime = DateTime.MinValue;

        public event EventHandler<Bitmap> FrameProcessed;
        public event EventHandler<double> FocusScoreUpdated;

        // 替换构造函数中的以下行：
        // _camera.StartCapture() += OnFrameCaptured;

        // 正确做法是订阅摄像头的帧捕获事件。假设 ICameraService 有一个 FrameCaptured 事件（如没有，需要在 ICameraService 中添加该事件）。
        // 例如：public event EventHandler<Bitmap> FrameCaptured;

        public VideoProcessingService(ICameraService camera,
            ILoggerService logger,
            IImageProcessingService imageProcessing)
        {
            _camera = camera;
            _logger = logger;
            _imageProcessing = imageProcessing;

            // 正确的事件订阅方式
            // 假设 ICameraService 有 FrameCaptured 事件
            _camera.FrameCaptured += OnFrameCaptured;
        }

        private void OnFrameCaptured(object? sender, Bitmap frame)
        {
            if (!_isProcessing || frame == null)
                return;

            _frameCounter++;
            // 计算处理间隔
            var now = DateTime.Now;

            if ((now - _lastFrameTime).TotalMilliseconds <= _frameInterval)
            {
                return; // 如果处理间隔未到，则跳过当前帧

                _frameCounter = 0;
                _lastFrameTime = now;
            }
            _lastFrameTime = now;
            // 处理图像
            using (var mat = (Bitmap)frame.Clone())
            {
                var clonedMat = mat.ToMat();
                // 计算焦点分数
                double focusScore = _imageProcessing.CalculateFocusScore(clonedMat);
                FocusScoreUpdated?.Invoke(this, focusScore);

                // 处理图像

                // 触发帧处理事件
                FrameProcessed?.Invoke(this, clonedMat.ToBitmap());
            }
        }

        public void StartProcessing(int fps = 20)
        {
            if (_isProcessing)
                return;

            _targetFps = Math.Clamp(fps, 1, 30);
            _isProcessing = true;

            _processingCts = new CancellationTokenSource();

            _camera.StartCapture();

            _logger.LogInformation($"Starting video processing at {_targetFps} FPS");
        }

        public void StopProcessing()
        {
            if (!_isProcessing)
                return;
            _isProcessing = false;
            _processingCts.Cancel();

            _camera.StopCapture();
            _camera.Close();

            _logger.LogInformation("Video processing stopped.");
        }

        public void SetFrameRate(int fps)
        {
            if (fps < 1 || fps > 30)
            {
                _logger.LogError("FPS must be between 1 and 30.");
                return;
            }
            _targetFps = fps;
            _camera.SetAcquisitionFrameRate(fps);

            _frameInterval = 1000.0 / _targetFps; // 计算每帧间隔时间（毫秒）
            _logger.LogInformation($"Frame rate set to {_targetFps} FPS");
        }


        public void Dispose()
        {
            StopProcessing();
            _camera.FrameCaptured -= OnFrameCaptured;
            //_processingCts.Dispose();

        }
    }
}
