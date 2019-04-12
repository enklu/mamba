using System;
using System.Collections.Generic;
using Enklu.Mamba.Network;
using Microsoft.Kinect;
using Serilog;

namespace Enklu.Mamba.Kinect
{
    /// <summary>
    /// Interfaces with Kinect and pushes data to Mycelium.
    /// </summary>
    public class KinectController : IDisposable
    {
        /// <summary>
        /// Configuration.
        /// </summary>
        private readonly KinectControllerConfiguration _config;

        /// <summary>
        /// Network interface.
        /// </summary>
        private readonly IMyceliumInterface _network;
        
        // TODO: Handle sensor disconnect/reconnect
        private KinectSensor _sensor;

        private BodyCapture _bodyCapture;
        private ColorCapture _colorCapture;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="network">The network.</param>
        public KinectController(
            KinectControllerConfiguration config,
            IMyceliumInterface network)
        {
            _config = config;
            _network = network;
        }

        /// <summary>
        /// Starts the Kinect controller.
        /// </summary>
        public void Start()
        {
            // TODO: implement!
            Sensor_Connected(KinectSensor.GetDefault());
        }
        
        /// <summary>
        /// <c>IDisposable</c> implementation.
        /// </summary>
        ~KinectController()
        {
            ReleaseUnmanagedResources();
        }

        /// <summary>
        /// <c>IDisposable</c> implementation.
        /// </summary>
        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// <c>IDisposable</c> implementation.
        /// </summary>
        private void ReleaseUnmanagedResources()
        {
            _bodyCapture?.Stop();
            _colorCapture?.Stop();
        }
        
        private void Sensor_Connected(KinectSensor sensor)
        {
            Log.Information("Kinect found.");
            _sensor = sensor;
            _sensor.Open();
            
            _bodyCapture?.Stop();
            _bodyCapture = new BodyCapture(_sensor);
            _bodyCapture.OnBodyDetected += Body_OnDetected;
            _bodyCapture.OnBodyUpdated += Body_OnUpdated;
            _bodyCapture.OnBodyLost += Body_OnLost;
            _bodyCapture.Start();

//            _colorCapture?.Stop();
//            _colorCapture = new ColorCapture(_sensor);
//            _colorCapture.OnImageReady += OnColorImage;
//            _colorCapture.Start();
        }

        private void Body_OnDetected(ulong id)
        {
            Log.Information("Body Detected: " + id);
        }

        private void Body_OnUpdated(ulong id, BodyCapture.SensorData data)
        {
            
        }

        private void Body_OnLost(ulong id)
        {
            Log.Information("Body Lost: " + id);
        }
    }
}