using System;
using Enklu.Mycerializer.Netty;

namespace Enklu.Mamba.Util
{
    /// <summary>
    /// Tracks network metrics.
    /// </summary>
    public class DummyNetworkMetricsProvider : INetworkMetricsProvider, IDisposable
    {
        /// <inheritdoc />
        public void TrackMessageSize(int size)
        {
            // 
        }

        /// <inheritdoc />
        public IDisposable TrackEncodeTime()
        {
            return this;
        }

        /// <inheritdoc />
        public IDisposable TrackDecodeTime()
        {
            return this;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // 
        }
    }
}