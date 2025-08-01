using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace DenseLight.Message
{
    public sealed class FrameUpdateMessage
    {
        public BitmapFrame Frame { get; set; }
        public FrameUpdateMessage(BitmapFrame frame) { Frame = frame; }
    }
}
