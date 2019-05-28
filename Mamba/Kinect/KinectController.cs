using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        /// <summary>
        /// Whether any data is currently being captured.
        /// </summary>
        private bool _active;
        
        /// <summary>
        /// Map of tracked body ID -> BodyElements. Keys will exist when a body is detected.
        /// Values will populate asynchronously after network ops.
        /// </summary>
        private readonly Dictionary<ulong, BodyElements> _bodyElements = new Dictionary<ulong, BodyElements>();

        /// <summary>
        /// The joints the current Kinect Element needs to track.
        /// </summary>
        private JointType[] _trackList;

        /// <summary>
        /// The corresponding Assets for each JointType.
        /// </summary>
        private Dictionary<JointType, string> _assetMap;

        /// <summary>
        /// Schema values.
        /// </summary>
        private const string PROP_VISIBLE = "visible";
        private const string PROP_POSITION = "position";
        private const string PROP_ROTATION = "rotation";

        /// <summary>
        /// Last time an update was sent upstream.
        /// </summary>
        private DateTime _lastUpdate = DateTime.MinValue;
        
        /// <summary>
        /// Batched update calls.
        /// </summary>
        private readonly List<ElementActionData> _updates = new List<ElementActionData>();

        /// <summary>
        /// Constructor.
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
            }
            
            _sensor.Close();
        }
        
        /// <summary>
        /// Called when the Kinect SDK changes its device availability. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void SensorOnIsAvailableChanged(object sender, IsAvailableChangedEventArgs args)
        {
            _bodyCapture?.Stop();
            
            if (args.IsAvailable)
            {
                Log.Information($"Kinect available ({_sensor.UniqueKinectId})");

                _kinectElement = Util.FindKinect(_sensor.UniqueKinectId, _elements);
                if (_kinectElement == null)
                {
                    Log.Warning("No Kinect element found in scene.");
                    return;
                }
                Log.Information($"Kinect element found ({_kinectElement})");

                _assetMap = Util.BuildTracking(_kinectElement);
                _trackList = _assetMap.Keys.Select(j => j).ToArray();
                if (_trackList.Length == 0)
                {
                    Log.Warning("No tracking desired?");
                    return;
                }
                Log.Information($"Tracking {_trackList.Length} joints " +
                                $"({string.Join(", ", _trackList.Select(j => j.ToString()))})");
                
                _bodyCapture = new BodyCapture(_sensor, _trackList);
                _bodyCapture.OnFrameStart += Body_OnFrameStart;
                _bodyCapture.OnFrameEnd += Body_OnFrameEnd;
                _bodyCapture.OnBodyDetected += Body_OnDetected;
                _bodyCapture.OnBodyUpdated += Body_OnUpdated;
                _bodyCapture.OnBodyLost += Body_OnLost;
                _bodyCapture.Start();

                _active = true;
            }
            else if (_active)
            {
                Log.Information("Lost connection to Kinect.");

                _kinectElement = null;

                foreach (var bodyElements in _bodyElements.Values)
                {
                    if (bodyElements != null) DestroyBody(bodyElements);
                }
                _bodyElements.Clear();
                
                _active = false;
            }
        }

        /// <summary>
        /// Invoked when a new frame of data comes from the Kinect.
        /// </summary>
        private void Body_OnFrameStart()
        {
            _updates.Clear();
        }

        /// <summary>
        /// Invoked when a frame completes from the Kinect.
        /// </summary>
        private void Body_OnFrameEnd()
        {
            if (_updates.Count > 0)
            {
                _network.Update(_updates.ToArray());
                _lastUpdate = DateTime.Now;
            }
        }

        /// <summary>
        /// Called when BodyCapture detects a new body.
        /// </summary>
        /// <param name="id">Unique ID of the body.</param>
        private void Body_OnDetected(ulong id)
        {
            Log.Information($"Body detected, creating body & joint elements (Body={id}).");

            // Register the slot, but don't populate until elements are created on the network.
            _bodyElements[id] = null;

            var bodyElements = Util.CreateBodyElements($"Body {id}", _assetMap);
            
            _network
                .Create(_kinectElement.Id, bodyElements.RootElement)
                .ContinueWith(task =>
                {
                    var rootElement = task.Result;
                    
                    Log.Information($"Created body root element (Body={id} Element={rootElement.Id}).");
                    
                    // Double check the body didn't disappear during the network op
                    if (!_bodyElements.ContainsKey(id))
                    {
                        Log.Information($"Matching body already gone. Destroying (Body={id} Element={rootElement.Id}).");
                        _network.Destroy(rootElement.Id);
                        return;
                    }
                    
                    _bodyElements[id] = bodyElements;
                });
        }

        /// <summary>
        /// Called when BodyCapture has updated data for a body.
        /// </summary>
        /// <param name="id">Unique ID of the body.</param>
        /// <param name="data">Current data for the body.</param>
        private void Body_OnUpdated(ulong id, BodyCapture.SensorData data)
        {
            if (!_bodyElements.TryGetValue(id, out var bodyElements))
            {
                Log.Error("Body updated, but was never tracked.");
                return;
            }
            
            if ((DateTime.Now - _lastUpdate).TotalMilliseconds < _config.SendIntervalMs)
            {
                return; // Time throttling
            }

            if (bodyElements == null)
            {
                return; // This is okay, Elements are in flight.
            }

            for (int i = 0, len = _trackList.Length; i < len; i++)
            {
                var jointType = _trackList[i];
                
                if (!data.JointPositions.ContainsKey(jointType))
                {
                    // If there's invalid data and the body is visible, hide it!
                    if (bodyElements.Visible)
                    {
                        _network.Update(new [] { new ElementActionData
                        {
                           ElementId = bodyElements.RootElement.Id,
                           Type = "update",
                           SchemaType = "bool",
                           Key = PROP_VISIBLE,
                           Value = false
                        }});
                        bodyElements.Visible = false;
                    }
                    return;
                }
                
                _updates.Add(new ElementActionData
                {
                    ElementId = bodyElements.JointElements[jointType].Id,
                    Type = "update",
                    SchemaType = "vec3",
                    Key = PROP_POSITION,
                    Value = data.JointPositions[jointType]
                });
                
                _updates.Add(new ElementActionData
                {
                    ElementId = bodyElements.JointElements[jointType].Id,
                    Type = "update",
                    SchemaType = "vec3",
                    Key = PROP_ROTATION,
                    Value = data.JointRotations[jointType]
                });
            }

            // If previously hidden, unhide!
            if (!bodyElements.Visible)
            {
                _updates.Add(new ElementActionData
                {
                    ElementId = bodyElements.RootElement.Id,
                    Type = "update",
                    SchemaType = "bool",
                    Key = PROP_VISIBLE,
                    Value = true
                });
            }
        }

        /// <summary>
        /// Called when BodyCapture loses a body.
        /// </summary>
        /// <param name="id">Unique ID of the body lost.</param>
        private void Body_OnLost(ulong id)
        {
            if (!_bodyElements.TryGetValue(id, out var bodyElements))
            {
                Log.Error("Body lost, but was never tracked.");
                return;
            }

            if (bodyElements == null)
            {
                Log.Information($"Body lost before Element created (Body={id})");
            }
            else
            {
                Log.Information("Body lost.");
                DestroyBody(bodyElements);
            }

            _bodyElements.Remove(id);
        }

        /// <summary>
        /// Destroys a body Element.
        /// </summary>
        /// <param name="body"></param>
        private void DestroyBody(BodyElements body)
        {
            Log.Information($"Destroying body (Element={body.RootElement.Id})");
            try
            {
                _network
                    .Destroy(body.RootElement.Id)
                    .ContinueWith(_ =>
                    {
                        Log.Information($"Destruction successful (Element={body.RootElement.Id}");
                    });
            }
            catch (Exception e)
            {
                Log.Error($"Error destroying body (Element={body.RootElement.Id}, Exception={e}");
            }
        }
    }
}