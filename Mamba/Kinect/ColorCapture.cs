using System;
using Microsoft.Kinect;

namespace Enklu.Mamba.Kinect
{
    public class ColorCapture
    {
        private readonly KinectSensor _sensor;
        private ColorFrameReader _reader;

        public Action<byte[]> OnImageReady;

        public ColorCapture(KinectSensor sensor)
        {
            _sensor = sensor;
        }

        public void Start()
        {
            _reader = _sensor.ColorFrameSource.OpenReader();
            _reader.FrameArrived += Reader_OnFrameArrived;
        }

        public void Stop()
        {
            _reader.Dispose();
        }

        private void Reader_OnFrameArrived(object obj, ColorFrameArrivedEventArgs args)
        {
            var frameRef = args.FrameReference;
            var colorFrame = frameRef?.AcquireFrame();

            if (colorFrame == null) return;

            var frameDescription = colorFrame.FrameDescription;

            var pixels = new byte[frameDescription.Width * frameDescription.Height * frameDescription.BytesPerPixel];
            if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
            {
                colorFrame.CopyRawFrameDataToArray(pixels);
            }
            else
            {
                colorFrame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);
            }
            
            OnImageReady?.Invoke(pixels);
            
            colorFrame.Dispose();
        }
    }
}