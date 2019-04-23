using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Elements.
        /// </summary>
        private readonly ElementData _elements;

        /// <summary>
        /// The element to use for tracking.
        /// </summary>
        private ElementData _kinectElement;
        
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
        
        private readonly Dictionary<ulong, Dictionary<JointType, Vec3>> _jointPositions = new Dictionary<ulong, Dictionary<JointType, Vec3>>();
        private readonly Dictionary<ulong, Dictionary<JointType, Vec3>> _jointRotations = new Dictionary<ulong, Dictionary<JointType, Vec3>>();

        private JointType[] _trackList;

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
        private const string PropKinectId = "kinect.id";
        private const string PropBodyPrefix = "kinect.body.";
        private const string PropVisible = "visible";
        private const string PropPosition = "position";
        private const string PropRotation = "rotation";
        
        private DateTime _lastUpdate = DateTime.Now;

        /// <summary>
        /// 
        /// </summary>
        public KinectController(
            KinectControllerConfiguration config,
            IMyceliumInterface network,
            ElementData elements)
        {
            _config = config;
            _network = network;
            _elements = elements;
        }

        /// <summary>
        /// Starts the Kinect controller.
        /// </summary>
        public void Start()
        {
            Log.Information("Waiting for Kinect...");
            
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
            // Kind of a hail mary. Network might have already been disposed :(
            HideElements();
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

        /// <summary>
        /// Sets the visibility of all pooled elements to false.
        /// </summary>
        private void HideElements()
        {
            var hideData = new ElementActionData[_elementPool.Count];

            for (var i = 0; i < _elementPool.Count; i++)
            {
                hideData[i] = new ElementActionData
                {
                    ElementId = _elementPool[i],
                    Type = "update",
                    SchemaType = "bool",
                    Key = PropVisible,
                    Value = false,
                };
            }
            
            _network.Update(hideData);
        }
        
        /// <summary>
        /// Called when the Kinect SDK changes its device availability. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void SensorOnIsAvailableChanged(object sender, IsAvailableChangedEventArgs args)
        {
            _bodyCapture?.Stop();
//            _colorCapture?.Stop();
            
            if (args.IsAvailable)
            {
                
                
                Log.Information($"Kinect available ({_sensor.UniqueKinectId})");

                _kinectElement = FindKinect(_sensor.UniqueKinectId, _elements);
                if (_kinectElement == null)
                {
                    Log.Warning("No Kinect element found in scene.");
                    return;
                }
                Log.Information($"Kinect element found ({_kinectElement})");

                _trackList = BuildTracking(_kinectElement);
                if (_trackList.Length == 0)
                {
                    Log.Warning("No tracking desired?");
                    return;
                }
                Log.Information($"Tracking {_trackList.Length} joints " +
                                $"({string.Join(", ", _trackList.Select(j => j.ToString()))})");
                
                
                _bodyCapture = new BodyCapture(_sensor, _trackList);
                _bodyCapture.OnBodyDetected += Body_OnDetected;
                _bodyCapture.OnBodyUpdated += Body_OnUpdated;
                _bodyCapture.OnBodyLost += Body_OnLost;
                _bodyCapture.Start();

//            _colorCapture = new ColorCapture(_sensor);
//            _colorCapture.OnImageReady += OnColorImage;
//            _colorCapture.Start();

                _active = true;
            }
            else if (_active)
            {
                Log.Information("Lost connection to Kinect.");

                _kinectElement = null;

                // TODO: Reset lookup tables
                HideElements();

                foreach (var id in _bodyElements.Values)
                {
                    _elementPool.Add(id);
                }
                _bodyElements.Clear();
                
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
            Log.Information($"Body detected (Id={id} Element={elementId})");
            
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
            if ((DateTime.Now - _lastUpdate).TotalMilliseconds < _config.SendIntervalMs)
            {
                return;
            }
            
            if (!_bodyElements.TryGetValue(id, out var elementId))
            {
                Log.Error("Body updated, but was never tracked.");
                return;
            }

            var updates = new ElementActionData[_trackList.Length * 2];

            for (int i = 0, len = _trackList.Length; i < len; i++)
            {
                var jointType = _trackList[i];
                
                if (!data.JointPositions.ContainsKey(jointType))
                {
                    // Early out if we can't determine a valid position.
                    // TODO: Toggle visibility when this happens.
                    return;
                }

                updates[i * 2] = new ElementActionData
                {
                    ElementId = elementId,
                    Type = "update",
                    SchemaType = "vec3",
                    Key = PropPosition,
                    Value = data.JointPositions[jointType]
                };

                updates[i * 2 + 1] = new ElementActionData
                {
                    ElementId = elementId,
                    Type = "update",
                    SchemaType = "vec3",
                    Key = PropRotation,
                    Value = data.JointRotations[jointType]
                };
            }
            
            _network.Update(updates);
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
            
            Log.Information($"Body lost (Id={id} Element={elementId})");

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

        /// <summary>
        /// Finds an element with a matching KinectId.
        /// </summary>
        /// <param name="kinectId"></param>
        /// <param name="element"></param>
        /// <returns></returns>
        private ElementData FindKinect(string kinectId, ElementData element)
        {
            element.Schema.Strings.TryGetValue(PropKinectId, out var schemaId);

            if (schemaId == kinectId)
            {
                return element;
            }
            
            for (int i = 0, len = element.Children.Length; i < len; i++)
            {
                if (FindKinect(kinectId, element.Children[i]) != null)
                {
                    return element.Children[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Determines which joints to track based on schema.
        /// </summary>
        /// <param name="elementData"></param>
        /// <returns></returns>
        private JointType[] BuildTracking(ElementData elementData)
        {
            var prefixLen = PropBodyPrefix.Length;

            var jointTypes = new List<JointType>();
            foreach (var kvp in elementData.Schema.Strings)
            {
                if (kvp.Key.StartsWith(PropBodyPrefix))
                {
                    var bodyPart = kvp.Key.Substring(prefixLen);
                    bodyPart = bodyPart.Substring(0, bodyPart.IndexOf(".", StringComparison.Ordinal));

                    if (Enum.TryParse<JointType>(bodyPart, true, out var jointType))
                    {
                        jointTypes.Add(jointType);
                    };
                }
            }

            return jointTypes.ToArray();
        }
    }
}