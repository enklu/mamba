using System;
using Enklu.Data;
using Microsoft.Kinect;

namespace Enklu.Mamba.Kinect
{
    public static class Math
    {
        private const float PI = (float) System.Math.PI;
        
        public static Vec3 QuatToEuler(Vector4 q)
        {
            double sqw = q.W * q.W;
            double sqx = q.X * q.X;
            double sqy = q.Y * q.Y;
            double sqz = q.Z * q.Z;
            
            var pitch = -(float) System.Math.Atan2(2 * ((q.Y * q.Z) + (q.W * q.X)), (q.W * q.W) - sqx - sqy + sqz);
            var yaw = (float) System.Math.Asin(2 * ((q.W * q.Y) - (q.X * q.Z)));
            var roll = (float) System.Math.Atan2(2 * ((q.X * q.Y) + (q.W * q.Z)), (q.W * q.W) + sqx - sqy - sqz);

            return new Vec3(
                pitch * (180 / PI),
                yaw * (180 / PI),
                roll * (180 / PI)
            );
        }
    }
}