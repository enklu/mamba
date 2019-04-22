using Enklu.Mamba.Kinect;

namespace Enklu.Mamba
{
    /// <summary>
    /// Configuration for Mamba.
    /// </summary>
    public class MambaConfiguration
    {
        public string MyceliumIp;
        public int MyceliumPort;
        public string TrellisUrl;
        public string Token;
        public string ExperienceId;

        /// <summary>
        /// Host to send graphite metrics to.
        /// </summary>
        public string GraphiteHost;

        /// <summary>
        /// Key to send metrics with.
        /// </summary>
        public string GraphiteKey;

        /// <summary>
        /// Kinect configuration.
        /// </summary>
        public readonly KinectControllerConfiguration Kinect = new KinectControllerConfiguration();

        /// <summary>
        /// Overrides settings.
        /// </summary>
        /// <param name="config">Configuration values to override with.</param>
        public void Override(MambaConfiguration config)
        {
            if (!string.IsNullOrEmpty(config.MyceliumIp))
            {
                MyceliumIp = config.MyceliumIp;
            }

            if (config.MyceliumPort > 0)
            {
                MyceliumPort = config.MyceliumPort;
            }

            if (!string.IsNullOrEmpty(config.TrellisUrl))
            {
                TrellisUrl = config.TrellisUrl;
            }

            if (!string.IsNullOrEmpty(config.Token))
            {
                Token = config.Token;
            }

            if (!string.IsNullOrEmpty(config.ExperienceId))
            {
                ExperienceId = config.ExperienceId;
            }

            if (!string.IsNullOrEmpty(config.GraphiteHost))
            {
                GraphiteHost = config.GraphiteHost;
            }

            if (!string.IsNullOrEmpty(config.GraphiteKey))
            {
                GraphiteKey = config.GraphiteKey;
            }

            if (config.Kinect != null)
            {
                Kinect.Override(config.Kinect);
            }
        }

        /// <summary>
        /// ToString.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return
                $"MyceliumIp: {MyceliumIp}, MyceliumPort: {MyceliumPort}, TrellisUrl: {TrellisUrl}, Token: {Token}, " +
                $"ExperienceId: {ExperienceId}, GraphiteHost: {GraphiteHost}, GraphiteKey: {GraphiteKey}";
        }
    }
}