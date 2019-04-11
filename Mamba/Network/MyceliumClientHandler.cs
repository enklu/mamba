using System;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Enklu.Mycelium.Messages;
using Serilog;

namespace Enklu.Mamba.Network
{
    public class MyceliumClientHandler : ChannelHandlerAdapter
    {
        private readonly string _token;

        private volatile bool _isAlive;
        private IChannelHandlerContext _context;

        public event Action<MyceliumClientHandler> OnDisconnected;

        public MyceliumClientHandler(string token)
        {
            _token = token;
        }

        public Task Close()
        {
            return CloseAsync(_context);
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);

            _isAlive = true;
            _context = context;

            Log.Information("Connected! Sending login request.");

            // send auth
            context.WriteAndFlushAsync(new LoginRequest
            {
                Jwt = _token
            });
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            base.ChannelInactive(context);

            _isAlive = false;

            Log.Information("Disconnected!");

            OnDisconnected?.Invoke(this);
        }

        public override void ChannelRead(
            IChannelHandlerContext context,
            object message)
        {
            base.ChannelRead(context, message);

            if (message is LoginResponse)
            {
                Log.Information("Logged in!");
            }
            else
            {
                Log.Warning($"Received unhandled message: {message}.");
            }
        }
    }
}