using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace DenseLight.Models
{
    public class DisplayFrame
    {
        public byte[] FrameObject { get; set; } = Array.Empty<byte>();

        public int Height { get; set; } = 0;

        public int Width { get; set; } = 0;

        public int Stride { get; set; } = 0;

        public byte Depth { get; set; } = 0;

        public byte Channels { get; set; } = 0;

        public Mat? Image { get; set; }

        public BitmapSource? Source { get; set; }
    }
}
