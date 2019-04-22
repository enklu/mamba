namespace Enklu.Mamba.Kinect
{
    /// <summary>
    /// Configuration for Kinect.
    /// </summary>
    public class KinectControllerConfiguration
    {
        /// <summary>
        /// The amount of time between sending network updates.
        /// </summary>
        public int SendIntervalMs = 16;
        
        /// <summary>
        /// The distance change required to trigger an update.
        /// </summary>
        public float DistanceThresholdM = 0.025f;
        
        /// <summary>
        /// The rotation change required to trigger an update.
        /// </summary>
        public float RotationThresholdDeg = 5;

        public void Override(KinectControllerConfiguration config)
        {
            if (config.SendIntervalMs > float.Epsilon)
            {
                SendIntervalMs = config.SendIntervalMs;
            }

            if (config.DistanceThresholdM > float.Epsilon)
            {
                DistanceThresholdM = config.DistanceThresholdM;
            }
            
            if (config.RotationThresholdDeg > float.Epsilon)
            {
                RotationThresholdDeg = config.RotationThresholdDeg;
            }
        }
    }
}