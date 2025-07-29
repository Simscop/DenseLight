using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace DenseLight.Services
{
    public interface IImageProcessingService
    {
        double CalculateFocusScore(Mat image);
        double CalculateFocusScore(Mat image, double cropSize);
    }
}
