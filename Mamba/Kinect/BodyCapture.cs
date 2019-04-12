using System;
using System.Collections.Generic;
using Enklu.Data;
using Microsoft.Kinect;
using Serilog;

namespace Enklu.Mamba.Kinect
{
    public class BodyCapture
    {
        public struct SensorData
        {
            public Dictionary<JointType, Vec3> JointPositions;
        }
        
        private readonly KinectSensor _sensor;
        private BodyFrameReader _reader;
        
        private readonly Body[] _bodyData = new Body[6];
        
        private readonly List<ulong> _trackedBodies = new List<ulong>();
        private List<ulong> _scratch = new List<ulong>();

        // TODO: Read from config
        private readonly JointType[] _jointsDesired;
        
        public Action<ulong> OnBodyDetected;
        public Action<ulong, SensorData> OnBodyUpdated;
        public Action<ulong> OnBodyLost;

        public BodyCapture(KinectSensor sensor)
        {
            _sensor = sensor;

            _jointsDesired = new[] { JointType.ShoulderLeft, JointType.ShoulderRight };
        }

        public void Start()
        {
            Log.Information("BodyCapture: Start");
            _reader = _sensor.BodyFrameSource.OpenReader();
            _reader.FrameArrived += Reader_OnFrameArrived;
        }

        public void Stop()
        {
            Log.Information("BodyCapture: Stop");
            _reader.Dispose();
        }

        private void Reader_OnFrameArrived(object obj, BodyFrameArrivedEventArgs args)
        {
            var frameRef = args.FrameReference;
            var bodyFrame = frameRef?.AcquireFrame();

            if (bodyFrame == null) return;
            
            bodyFrame.GetAndRefreshBodyData(_bodyData);
        
            _scratch = new List<ulong>(_trackedBodies);
            for (var i = 0; i < _bodyData.Length; i++)
            {
                var body = _bodyData[i];
                if (body != null && body.IsTracked)
                {
                    var bodyId = body.TrackingId;
                    
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
                        _scratch.Remove(bodyId);
                    }

                    var data = new SensorData
                    {
                        JointPositions = new Dictionary<JointType, Vec3>()
                    };

                    for (var j = 0; j < _jointsDesired.Length; j++)
                    {
                        var type = _jointsDesired[j];
                        var joint = body.Joints[type];
                        if (joint.TrackingState != TrackingState.NotTracked)
                        {
                            data.JointPositions[type] = joint.Position;
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

            while (_scratch.Count > 0)
            {
                try
                {
                    OnBodyLost?.Invoke(_scratch[0]);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error invoking BodyLost: " + e);
                }
                
                _scratch.RemoveAt(0);
            }
            
            bodyFrame.Dispose();
        }
    }

    public struct Vec3
    {
        public float X;
        public float Y;
        public float Z;
        
        public static implicit operator Vec3(CameraSpacePoint cameraSpacePoint)
        {
            return new Vec3
            {
                X = -cameraSpacePoint.X,
                Y = cameraSpacePoint.Y,
                Z = cameraSpacePoint.Z
            };
        }
    }
}