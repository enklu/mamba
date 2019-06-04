using System;
using DotNetty.Transport.Channels;
using Enklu.Mycelium.Messages;
using Enklu.Mycelium.Util;
using Serilog;

namespace Enklu.Mamba.Network
{
    /// <summary>
    /// A heartbeat that times out.
    /// </summary>
    /// <inheritdoc />
    public class KeepAliveThrottle : IDisposable
    {
        private readonly IChannel _channel;
        private readonly AsyncTimer _timer;

        private DateTimeOffset _lastSent;
        private DateTimeOffset _lastReceived;

        private PingRequest _outboundRequest;
        private byte _pingId;

        public KeepAliveThrottle(IChannel channel)
        {
            _channel = channel;
            _timer = new AsyncTimer(OnTick, TimeSpan.FromSeconds(1));
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void OnTick()
        {
            var now = DateTimeOffset.UtcNow;
            if (null == _outboundRequest)
            {
                if (now - _lastReceived >= TimeSpan.FromSeconds(5))
                {
                    _outboundRequest = new PingRequest { PingId = ++_pingId };
                    _lastSent = DateTimeOffset.UtcNow;

                    _channel.WriteAndFlushAsync(_outboundRequest);
                }

                return;
            }

            if (now - _lastSent >= TimeSpan.FromSeconds(5))
            {
                L("PingResponse Timeout Occurred");

                _channel.CloseAsync();
                _timer.Stop();
            }
        }

        public void Received(PingResponse response)
        {
            if (null == _outboundRequest)
            {
                return;
            }
            
            if (response.PingId == _outboundRequest.PingId)
            {
                _lastReceived = DateTimeOffset.UtcNow;
                _outboundRequest = null;
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        private void L(string message)
        {
            Log.Information($"[KeepAliveThrottle-{_channel?.Id?.AsShortText() ?? "N/A"}]: {message}");
        }
    }
}