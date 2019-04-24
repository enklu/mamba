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
        private class BodyElements
        {
            public ElementData RootElement;
            public readonly Dictionary<JointType, ElementData> JointElements = new Dictionary<JointType, ElementData>();

            public readonly Dictionary<JointType, Vec3> JointPositions = new Dictionary<JointType, Vec3>();
            public readonly Dictionary<JointType, Vec3> JointRotations = new Dictionary<JointType, Vec3>();
        }
        
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

        private JointType[] _trackList;

        private Dictionary<JointType, string> _assetMap;

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
            
            // TODO: For testing!
            Body_OnDetected(8675309);
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
            }
            
            _sensor.Close();
        }

        /// <summary>
        /// Sets the visibility of all pooled elements to false.
        /// </summary>
        private void HideElements()
        {
//            var hideData = new ElementActionData[_elementPool.Count];
//
//            for (var i = 0; i < _elementPool.Count; i++)
//            {
//                hideData[i] = new ElementActionData
//                {
//                    ElementId = _elementPool[i],
//                    Type = "update",
//                    SchemaType = "bool",
//                    Key = PropVisible,
//                    Value = false,
//                };
//            }
//            
//            _network.Update(hideData);
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

                _kinectElement = FindKinect(_sensor.UniqueKinectId, _elements);
                if (_kinectElement == null)
                {
                    Log.Warning("No Kinect element found in scene.");
                    return;
                }
                Log.Information($"Kinect element found ({_kinectElement})");

                _assetMap = BuildTracking(_kinectElement);
                _trackList = _assetMap.Keys.Select(j => j).ToArray();
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

                _active = true;
            }
            else if (_active)
            {
                Log.Information("Lost connection to Kinect.");

                _kinectElement = null;

                // TODO: Reset lookup tables

                foreach (var bodyElements in _bodyElements.Values)
                {
                    if (bodyElements != null) _network.Destroy(bodyElements.RootElement.Id);
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
            Log.Information($"Body detected (Body={id})");
            _bodyElements[id] = null;
            
            _network
                .Create(_kinectElement.Id, Util.CreateElementData($"Body {id}"))
                .ContinueWith(task =>
                {
                    var rootElement = task.Result;
                    
                    Log.Information($"Element created (Body={id} Element={rootElement.Id})");
                    
                    // Double check the body didn't disappear during the network op
                    if (!_bodyElements.ContainsKey(id))
                    {
                        Log.Information($"Matching body already gone. Destroying (Body={id} Element={rootElement.Id})");
                        _network.Destroy(rootElement.Id);
                        return;
                    }
                    
                    var bodyElements = new BodyElements();
                    bodyElements.RootElement = rootElement;

                    var jointCreates = new Task<ElementData>[_trackList.Length];
                    for (int i = 0, len = _trackList.Length; i < len; i++)
                    {
                        var jointType = _trackList[i];
                        jointCreates[i] = _network.Create(_kinectElement.Id, // TODO: Switch to rootElement.Id after bug is fixed 
                            Util.CreateElementData(jointType.ToString(), _assetMap[jointType]));
                    }

                    Task.WhenAll(jointCreates)
                        .ContinueWith(_ =>
                        {
                            Log.Information($"All joint elements created (Element={rootElement.Id})");
                            
                            
                            for (int i = 0, len = _trackList.Length; i < len; i++)
                            {
                                bodyElements.JointElements[_trackList[i]] = jointCreates[i].Result;
                            }

                            _bodyElements[id] = bodyElements;
                        });
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
            
            if (!_bodyElements.TryGetValue(id, out var bodyElements))
            {
                Log.Error("Body updated, but was never tracked.");
                return;
            }

            if (bodyElements == null)
            {
                return; // This is okay, Elements are in flight.
            }

            const int stride = 2;
            var updates = new ElementActionData[_trackList.Length * stride];

            for (int i = 0, len = _trackList.Length; i < len; i++)
            {
                var jointType = _trackList[i];
                
                if (!data.JointPositions.ContainsKey(jointType))
                {
                    // Early out if we can't determine a valid position.
                    // TODO: Toggle visibility when this happens.
                    return;
                }

                updates[i * stride] = new ElementActionData
                {
                    ElementId = bodyElements.JointElements[jointType].Id,
                    Type = "update",
                    SchemaType = "vec3",
                    Key = PropPosition,
                    Value = data.JointPositions[jointType]
                };

                updates[i * stride + 1] = new ElementActionData
                {
                    ElementId = bodyElements.JointElements[jointType].Id,
                    Type = "update",
                    SchemaType = "vec3",
                    Key = PropRotation,
                    Value = data.JointRotations[jointType]
                };
            }
            
            _network.Update(updates);
            _lastUpdate = DateTime.Now;
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
                Log.Information($"Body lost. Destroying element (Body={id}, Element={bodyElements.RootElement.Id})");
                _network.Destroy(bodyElements.RootElement.Id);
            }

            _bodyElements.Remove(id);
        }

        /// <summary>
        /// Finds an element with a matching KinectId.
        /// </summary>
        /// <param name="kinectId"></param>
        /// <param name="element"></param>
        /// <returns></returns>
        private static ElementData FindKinect(string kinectId, ElementData element)
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
        /// Determines which joints to track based on schema, and their corresponding assets
        /// </summary>
        /// <param name="elementData"></param>
        /// <returns></returns>
        private static Dictionary<JointType, string> BuildTracking(ElementData elementData)
        {
            var prefixLen = PropBodyPrefix.Length;

            var jointMap = new Dictionary<JointType, string>();
            foreach (var kvp in elementData.Schema.Strings)
            {
                if (kvp.Key.StartsWith(PropBodyPrefix))
                {
                    var bodyPart = kvp.Key.Substring(prefixLen);
                    bodyPart = bodyPart.Substring(0, bodyPart.IndexOf(".", StringComparison.Ordinal));

                    if (Enum.TryParse<JointType>(bodyPart, true, out var jointType))
                    {
                        jointMap[jointType] = kvp.Value;
                    }
                }
            }

            return jointMap;
        }
    }
}