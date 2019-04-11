using System;
using Enklu.Mycerializer.Netty;

namespace Enklu.Mamba.Util
{
    public class DummyNetworkMetricsProvider : INetworkMetricsProvider, IDisposable
    {
        public void TrackMessageSize(int size)
        {

        }

        public IDisposable TrackEncodeTime()
        {
            return this;
        }

        public IDisposable TrackDecodeTime()
        {
            return this;
        }

        public void Dispose()
        {
            //
        }
    }
}