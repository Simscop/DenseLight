using OpenCvSharp;
using System;
using System.Drawing;
using System.Windows.Media;

namespace DenseLight.Services
{
    public interface ICameraService
    {
        /// <summary>
        /// 输出连接状态
        /// </summary>
        /// <returns></returns>
        public void Configure();

        /// <summary>
        /// 相机初始化
        /// note: Deinit的代码写到析构函数中
        /// </summary>
        /// <returns></returns>
        public bool Init();
        /// <summary>
        /// 打开相机，之后配置相机参数
        /// </summary>
        public void Open();

        /// <summary>
        /// 关闭相机
        /// </summary>
        public void Close();

        /// <summary>
        /// 开始采集
        /// </summary>
        /// <returns></returns>
        public void StartCapture();

        /// <summary>
        /// 停止采集
        /// </summary>
        /// <returns></returns>
        public void StopCapture();

        /// <summary>
        /// 采集一帧图像
        /// </summary>
        /// <returns></returns>
        public bool Capture(out Mat mat);

        /// <summary>
        /// 保存一帧的图像
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public void SaveCapture(string path);

        /// <summary>
        /// 获取当前曝光
        /// </summary>
        /// <returns></returns>
        public string GetExposure();


        /// <summary>
        /// 获取当前最大曝光
        /// </summary>
        /// <returns></returns>
        public bool GetExposureMax(ref double exposureMax);


        /// <summary>
        /// 获取当前最小曝光
        /// </summary>
        /// <returns></returns>
        public bool GetExposureMin(ref double exposureMin);

        /// <summary>
        /// 设置曝光
        /// </summary>
        /// <param name="exposure"></param>
        /// <returns></returns>
        public bool SetExposure(float exposure);

        /// <summary>
        /// 获取图像帧率
        /// </summary>
        /// <returns></returns>
        public string GetFrameRate();

        /// <summary>
        /// 获取当前增益
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public string GetGain();


        public string GetPixelFormat();

        /// <summary>
        /// 设置增益
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetGain(float value);

        /// <summary>
        /// 设置采集帧率
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetAcquisitionFrameRate(float value);

        public event EventHandler<Bitmap> FrameCaptured;
    }
}
