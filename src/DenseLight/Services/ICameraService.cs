using System.Drawing;
using System.Windows.Media;

namespace DenseLight.Services
{
    public interface ICameraService
    {
        void InitializeCamera();
        Bitmap CaptureImage();

        void StartContinuousCapture();

        void StopContinuousCapture();

        void SetExposureTime(int exposureTimeMs);

        void DisconnectCamera();


    }
}
