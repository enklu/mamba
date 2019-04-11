using System;
using DotNetty.Transport.Channels;
using Enklu.Data;
using Enklu.Mycelium.Messages;
using Enklu.Mycelium.Messages.Experience;
using Serilog;

namespace Enklu.Mamba.Network
{
    /// <summary>
    /// Handles mycelium needs.
    /// </summary>
    public class MyceliumClientHandler : ChannelHandlerAdapter
    {
        /// <summary>
        /// The token to login with.
        /// </summary>
        private readonly string _token;

        /// <summary>
        /// True iff we are currently connected.
        /// </summary>
        private volatile bool _isAlive;

        /// <summary>
        /// The context with which to send messages.
        /// </summary>
        private IChannelHandlerContext _context;

        public ElementMap Map { get; private set; }

        /// <summary>
        /// Called when we disconnect.
        /// </summary>
        public event Action<MyceliumClientHandler> OnDisconnected;

        /// <summary>
        /// Controller.
        /// </summary>
        /// <param name="token">The token to login with.</param>
        public MyceliumClientHandler(string token)
        {
            _token = token;

            Map = new ElementMap();
        }

        /// <summary>
        /// Sends a message. Will throw if not connected.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void Send(object message)
        {
            _context.WriteAndFlushAsync(message);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public override void ChannelInactive(IChannelHandlerContext context)
        {
            base.ChannelInactive(context);

            _isAlive = false;

            Log.Information("Disconnected!");

            OnDisconnected?.Invoke(this);
        }

        /// <inheritdoc />
        public override void ChannelRead(
            IChannelHandlerContext context,
            object message)
        {
            base.ChannelRead(context, message);

            if (message is LoginResponse)
            {
                Log.Information("Logged in!");
            }
            else if (message is SceneDiffEvent diff)
            {
                Log.Information("Recieved scene diff event.");

                Map = diff.Map;
            }
            else
            {
                Log.Warning($"Received unhandled message: {message}.");
            }
        }
    }
}