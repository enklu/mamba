using System;
using System.Collections.Generic;
using Enklu.Data;
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
        
        /// <summary>
        /// Kinect Sensor interface. Will exist and be interactable, even with no Kinect present.
        /// </summary>
        private KinectSensor _sensor;

        /// <summary>
        /// Data Capture.
        /// </summary>
        private BodyCapture _bodyCapture;
        private ColorCapture _colorCapture;

        /// <summary>
        /// Whether any data is currently being captured.
        /// </summary>
        private bool _active;
        
        /// <summary>
        /// Map of tracked body ID -> Element Id.
        /// </summary>
        private readonly Dictionary<ulong, string> _bodyElements = new Dictionary<ulong, string>();

        /// <summary>
        /// Available Elements, hardcoded for now.
        /// </summary>
        private readonly List<string> _elementPool = new List<string>()
        {
            "52dac9cf-8e49-4d0b-af65-3b50ec988146",
            "24bebdb1-fa1b-41a8-9215-d424965a1c90",
            "c2d81517-37be-4d13-87bc-6bc37bff87e5",
            "4775fbc9-94ea-4307-84a5-fa7a32797f88",
            "68082549-3c0c-461d-9ddc-4ad0d293405a",
            "2b64bb7a-4fff-4b4e-95d4-5f0c0d31d8ff"
        };

        /// <summary>
        /// Schema values.
        /// </summary>
        private const string PropPosition = "position";
        private const string PropVisible = "visible";    // Doesn't show up in EnkluPlayer's prop hash map

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
            _sensor = KinectSensor.GetDefault();
            _sensor.IsAvailableChanged += SensorOnIsAvailableChanged;
            
            // Super unintuitive - even with no Kinect plugged in, the KinectSensor instance has to be opened
            // to detect a sensor being plugged in at a later time.
            _sensor.Open();
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
            if (_active)
            {
                _bodyCapture?.Stop();
                _colorCapture?.Stop();
            }
            
            _sensor.Close();
        }
        
        private void SensorOnIsAvailableChanged(object sender, IsAvailableChangedEventArgs args)
        {
            if (args.IsAvailable)
            {
                Log.Information("Kinect available: " + _sensor.UniqueKinectId);
                
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

                _active = true;
            }
            else if (_active)
            {
                Log.Information("Lost connection to Kinect.");
                _bodyCapture?.Stop();
//                _colorCapture?.Stop();

                // TODO: Hide elements, reset lookup tables

                _active = false;
            }
        }

        /// <summary>
        /// Called when BodyCapture detects a new body.
        /// </summary>
        /// <param name="id">Unique ID of the body.</param>
        private void Body_OnDetected(ulong id)
        {
            if (_elementPool.Count == 0)
            {
                Log.Error("New body but no free Element. Did the Kinect get an upgrade?!");
                return;
            }

            var elementId = _elementPool[0];
            Log.Information("Body Detected (Id={0} Element={1})", id, elementId);
            
            // Remove from pool, add reservation.
            _bodyElements[id] = elementId;
            _elementPool.RemoveAt(0);
            
            _network.Update(new []
            {
                new ElementActionData
                {
                    ElementId = elementId,
                    Type = "update",
                    SchemaType = "bool",
                    Key = PropVisible,
                    Value = true,
                }, 
            });
        }

        /// <summary>
        /// Called when BodyCapture has updated data for a body.
        /// </summary>
        /// <param name="id">Unique ID of the body.</param>
        /// <param name="data">Current data for the body.</param>
        private void Body_OnUpdated(ulong id, BodyCapture.SensorData data)
        {
            if (!_bodyElements.TryGetValue(id, out var elementId))
            {
                Log.Error("Body updated, but was never tracked.");
                return;
            }

            // TODO: Read from a joint config.
            if (!data.JointPositions.ContainsKey(JointType.ShoulderLeft) ||
                !data.JointPositions.ContainsKey(JointType.ShoulderRight))
            {
                // Early out if we can't determine a valid position.
                // TODO: Toggle visibility when this happens.
                return;
            }

            var shoulderLeft = data.JointPositions[JointType.ShoulderLeft];
            var shoulderRight = data.JointPositions[JointType.ShoulderRight];
            var midPoint = new Vec3
            {
                X = (shoulderLeft.X + shoulderRight.X) / 2,
                Y = (shoulderLeft.Y + shoulderRight.Y) / 2,
                Z = (shoulderLeft.Z + shoulderRight.Z) / 2
            };
            
            // TODO: Offset along cross product by 0.2m
            
            _network.Update(new []
            {
                new ElementActionData
                {
                    ElementId = elementId,
                    Type = "update",
                    SchemaType = "vec3",
                    Key = PropPosition,
                    Value = midPoint
                }, 
            });
        }

        /// <summary>
        /// Called when BodyCapture loses a body.
        /// </summary>
        /// <param name="id">Unique ID of the body lost.</param>
        private void Body_OnLost(ulong id)
        {
            if (!_bodyElements.TryGetValue(id, out var elementId))
            {
                Log.Error("Body lost, but was never tracked.");
                return;
            }
            
            Log.Information("Body Lost (Id={0} Element={1})", id, elementId);

            // Remove reservation, add back to the pool.
            _bodyElements.Remove(id);
            _elementPool.Add(elementId);
            
            _network.Update(new []
            {
                new ElementActionData
                {
                    ElementId = elementId,
                    Type = "update",
                    SchemaType = "bool",
                    Key = PropVisible,
                    Value = false
                }, 
            });
        }
    }
}