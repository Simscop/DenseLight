using DenseLight.Services;
using Lift.Core.Autofocus;
using OpenCvSharp;

namespace DenseLight.BusinessLogic
{
    public class ImageProcessingService : IImageProcessingService
    {
        private readonly ILoggerService _logger;
        public ImageProcessingService(ILoggerService logger)
        {
            _logger = logger;
        }
        public double CalculateFocusScore(Mat image) => CalculateFocusScore(image, 0.5);
        public double CalculateFocusScore(Mat image, double cropSize)
        {
            if (image == null || image.Empty())
            {
                _logger.LogError("Invalid image provided for focus score calculation.");
                return 0.0;
            }
            _logger.LogInformation("Calculating focus score with crop size: " + cropSize);
            // 计算裁剪区域
            int cropWidth = (int)(image.Width * cropSize);
            int cropHeight = (int)(image.Height * cropSize);
            int startX = (image.Width - cropWidth) / 2;
            int startY = (image.Height - cropHeight) / 2;

            if (cropWidth <= 0 || cropHeight <= 0 || startX <= 0 || startY <= 0)
            {
                _logger.LogError("Invalid crop parameters:  " + cropSize);
                return 0.0;
            }
            // 裁剪图像
            using (var roi = new Mat(image, new Rect(startX, startY, cropWidth, cropHeight)))
            {
                bool isLapLas = false;

                if (isLapLas)
                {
                    // 计算图像的拉普拉斯变换
                    Mat laplacian = new Mat();
                    Cv2.Laplacian(roi, laplacian, MatType.CV_64F);
                    // 计算拉普拉斯变换的方差
                    Scalar mean, stddev;
                    Cv2.MeanStdDev(laplacian, out mean, out stddev);
                    // 返回方差作为焦点分数
                    double focusScore = stddev[0] * stddev[0];
                    _logger.LogInformation("Focus score calculated using Laplacian: " + focusScore);
                    return focusScore;
                }
                else
                {
                    using (var processed = new Mat())
                    {
                        Cv2.MedianBlur(roi, processed, 3); // 使用中值滤波处理图像
                        Mat kernal = new float[9] { 2f, 1f, 0f, 1f, 0f, -1f, 0f, -1f, -2f }.ToKernel();

                        // 应用卷积核
                        using (var convolved = new Mat())
                        {
                            Cv2.Filter2D(processed, convolved, -1, kernal);

                            using (var squared = convolved.Mul(convolved))
                            {
                                Scalar sum = Cv2.Sum(squared);
                                double score = sum.Val0; // 计算平方和

                                _logger.LogDebug($"Focus score calculated: {score}");
                                return score;
                            }

                        }

                    }


                }



            }

        }

    }
    // 将扩展方法 ToKernel 移动到一个非泛型静态类中
    public static class KernelExtensions
    {
        //
        // 摘要:
        //     核函数转换 行优先
        public static Mat ToKernel(this float[] kernel)
        {
            int num = (int)Math.Sqrt(kernel.Length);
            Mat mat = new Mat(num, num, MatType.CV_32FC1);
            for (int i = 0; i < num; i++)
            {
                for (int j = 0; j < num; j++)
                {
                    mat.Set(i, j, kernel[i * num + j]);
                }
            }

            return mat;
        }
    }


}
