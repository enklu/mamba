using System;
using System.Collections.Generic;
using Enklu.Data;
using Microsoft.Kinect;
using Serilog;

namespace Enklu.Mamba.Kinect
{
    /// <summary>
    /// Reads configurable Body data from a Kinect.
    /// </summary>
    public class BodyCapture
    {
        /// <summary>
        /// Per-body data.
        /// </summary>
        public struct SensorData
        {
            public Dictionary<JointType, Vec3> JointPositions;
            public Dictionary<JointType, Vec3> JointRotations;
        }
        
        /// <summary>
        /// Kinect API.
        /// </summary>
        private readonly KinectSensor _sensor;
        
        /// <summary>
        /// Active body data reader.
        /// </summary>
        private BodyFrameReader _reader;
        
        /// <summary>
        /// Stores body data as it comes in.
        /// </summary>
        private readonly Body[] _bodyData = new Body[6];
        
        /// <summary>
        /// The current known body IDs.
        /// </summary>
        private readonly List<ulong> _trackedBodies = new List<ulong>();
        
        /// <summary>
        /// The current body IDs per update cycle.
        /// </summary>
        private List<ulong> _scratch = new List<ulong>();

        /// <summary>
        /// The list of joints to track.
        /// </summary>
        private readonly JointType[] _trackList;
        
        /// <summary>
        /// Invoked when a new body is detected.
        /// </summary>
        public Action<ulong> OnBodyDetected;
        
        /// <summary>
        /// Invoked when a body data is available.
        /// </summary>
        public Action<ulong, SensorData> OnBodyUpdated;
        
        /// <summary>
        /// Invoked when a body is lost.
        /// </summary>
        public Action<ulong> OnBodyLost;

        /// <summary>
        /// Constructor.
        /// </summary>
        public BodyCapture(KinectSensor sensor, JointType[] trackList)
        {
            _sensor = sensor;
            _trackList = trackList;
        }

        /// <summary>
        /// Starts reading data from the Kinect.
        /// </summary>
        public void Start()
        {
            Log.Information("BodyCapture: Start");
            _reader = _sensor.BodyFrameSource.OpenReader();
            _reader.FrameArrived += Reader_OnFrameArrived;
        }

        /// <summary>
        /// Stops reading data from the Kinect.
        /// </summary>
        public void Stop()
        {
            Log.Information("BodyCapture: Stop");
            _reader.Dispose();
        }

        /// <summary>
        /// Invoked when Body data is available.
        /// </summary>
        private void Reader_OnFrameArrived(object obj, BodyFrameArrivedEventArgs args)
        {
            var frameRef = args.FrameReference;
            var bodyFrame = frameRef?.AcquireFrame();

            if (bodyFrame == null) return;
            
            bodyFrame.GetAndRefreshBodyData(_bodyData);
        
            // Scratch list maintains current bodies, and removes them as they're updated.
            _scratch = new List<ulong>(_trackedBodies);
            for (var i = 0; i < _bodyData.Length; i++)
            {
                var body = _bodyData[i];
                if (body != null && body.IsTracked)
                {
                    var bodyId = body.TrackingId;
                    
                    // Handle new bodies
                    if (!_scratch.Contains(bodyId))
                    {
                        try
                        {
                            OnBodyDetected?.Invoke(bodyId);
                            _trackedBodies.Add(bodyId);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error invoking BodyDetected: " + e);
                        }
                    }
                    else
                    {
                        // Remove existing ones from scratch
                        _scratch.Remove(bodyId);
                    }

                    var data = new SensorData
                    {
                        JointPositions = new Dictionary<JointType, Vec3>(),
                        JointRotations = new Dictionary<JointType, Vec3>()
                    };

                    for (var j = 0; j < _trackList.Length; j++)
                    {
                        var type = _trackList[j];
                        var joint = body.Joints[type];
                        
                        if (joint.TrackingState != TrackingState.NotTracked)
                        {
                            data.JointPositions[type] = new Vec3(-joint.Position.X, joint.Position.Y, joint.Position.Z);
                            data.JointRotations[type] = Math.QuatToEuler(body.JointOrientations[type].Orientation);
                        }
                    }
                    
                    try
                    {
                        OnBodyUpdated?.Invoke(bodyId, data);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error invoking BodyUpdated: " + e);
                    }
                }
            }

            // Leftover scratch entries are lost bodies. Remove tracking & notify listeners.
            for (var i = 0; i < _scratch.Count; i++)
            {
                var id = _scratch[i];
                
                try
                {
                    OnBodyLost?.Invoke(id);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error invoking BodyLost: " + e);
                }
                
                _trackedBodies.Remove(id);
            }
            
            bodyFrame.Dispose();
        }
    }
}