using System;
using Enklu.Data;
using Microsoft.Kinect;

namespace Enklu.Mamba.Kinect
{
    /// <summary>
    /// Math helpers.
    /// </summary>
    public static class Math
    {
        /// <summary>
        /// You know it, you love it, it's PI!
        /// </summary>
        private const float PI = (float) System.Math.PI;
        
        /// <summary>
        /// Converts a quaternion into a Vec3 representing yaw/pitch/roll.
        /// </summary>
        /// <param name="q"></param>
        /// <returns></returns>
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