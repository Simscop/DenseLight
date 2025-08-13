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
                var FocusMethod = "Sobel";

                switch (FocusMethod)
                {
                    case "Laplacian":
                        using (var laplacian = new Mat())
                        {
                            // 使用拉普拉斯算子计算图像的二阶导数
                            // CV_64F 表示输出图像的深度为64位浮点数
                            // roi 是裁剪后的图像区域
                            Cv2.Laplacian(roi, laplacian, MatType.CV_64F);
                            // 计算拉普拉斯变换的方差
                            Scalar mean, stddev;
                            Cv2.MeanStdDev(laplacian, out mean, out stddev);
                            // 返回方差作为焦点分数
                            double focusScore = stddev[0] * stddev[0];
                            _logger.LogInformation("Focus score calculated using Laplacian: " + focusScore);
                            return focusScore;
                        }
                        break;

                    case "CustomMethod":

                        Mat KernalHorizontal = new float[9] { 1f, 1f, 1f, 0f, 0f, 0f, -1f, -1f, -1f }.ToKernel();
                        Mat KernalVertical = new float[9] { 1f, 0f, -1f, 1f, 0f, -1f, 1f, 0f, -1f }.ToKernel();

                        using (var processed = new Mat())
                        {
                            Cv2.MedianBlur(roi, processed, 3); // 使用中值滤波处理图像
                            using (var convolvedHorizontal = new Mat())
                            {
                                Cv2.Filter2D(processed, convolvedHorizontal, -1, KernalHorizontal);
                                using (var convolvedVertical = new Mat())
                                {
                                    Cv2.Filter2D(processed, convolvedVertical, -1, KernalVertical);
                                    using (var squaredHorizontal = convolvedHorizontal.Mul(convolvedHorizontal))
                                    using (var squaredVertical = convolvedVertical.Mul(convolvedVertical))
                                    {
                                        Scalar sumH = Cv2.Sum(squaredHorizontal);
                                        Scalar sumV = Cv2.Sum(squaredVertical);
                                        double score = sumH.Val0 + sumV.Val0; // 计算平方和
                                        _logger.LogInformation($"Focus score calculated: {score}");
                                        return score;
                                    }
                                }
                            }
                        }
                        break;

                    case "Sobel":

                        using (var processed = new Mat())
                        {
                            // 预处理：高斯模糊降噪
                            Cv2.GaussianBlur(roi, processed, new Size(3, 3), 0.8);

                            using (var gradX = new Mat())
                            using (var gradY = new Mat())
                            {
                                // 计算水平和垂直梯度
                                Cv2.Sobel(processed, gradX, MatType.CV_64F, 1, 0, ksize: 3);  // 水平
                                Cv2.Sobel(processed, gradY, MatType.CV_64F, 0, 1, ksize: 3);  // 垂直

                                // 计算梯度幅值 (平方和开方)
                                //using (var gradMag = new Mat())
                                //{
                                //    Cv2.Magnitude(gradX, gradY, gradMag);

                                //    // 计算梯度幅值的平方和作为分数
                                //    using (var gradSquared = new Mat())
                                //    {
                                //        Cv2.Pow(gradMag, 2, gradSquared);
                                //        Scalar sum = Cv2.Sum(gradSquared);
                                //        return sum.Val0;
                                //    }
                                //}

                                // 在计算梯度后直接使用：
                                using (var gradX2 = gradX.Mul(gradX))  // X方向平方
                                using (var gradY2 = gradY.Mul(gradY))  // Y方向平方
                                using (var energyMap = new Mat())
                                {
                                    Cv2.Add(gradX2, gradY2, energyMap);  // Ex² + Ey²
                                    return Cv2.Sum(energyMap)[0];
                                }
                            }
                        }

                        break;

                    default:

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

                        break;

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
