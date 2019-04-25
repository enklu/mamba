using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        /// Sources for req/res mapping.
        /// </summary>
        private readonly Dictionary<int, TaskCompletionSource<RoomResponse>> _sources = new Dictionary<int, TaskCompletionSource<RoomResponse>>();

        /// <summary>
        /// The context with which to send messages.
        /// </summary>
        private IChannelHandlerContext _context;

        /// <summary>
        /// Maps from id to hash.
        /// </summary>
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

        /// <summary>
        /// Sends a message and calls the callback when the response is received.
        /// </summary>
        /// <param name="req">The request to send.</param>
        public Task<RoomResponse> SendRequest(RoomRequest req)
        {
            var source = new TaskCompletionSource<RoomResponse>();

            lock (_sources)
            {
                _sources[req.RequestId] = source;
            }

            Send(req);

            return source.Task;
        }

        /// <inheritdoc />
        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);
            
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
                Log.Information("Received scene diff event.");

                Map = diff.Map;
            }
            else if (message is SceneMapUpdateEvent mapUpdate)
            {
                Log.Information($"Received scene map update event.");

                var elements = Map.Elements.ToList();
                elements.AddRange(mapUpdate.ElementsAdded);

                var props = Map.Props.ToList();
                props.AddRange(mapUpdate.PropsAdded);

                Map.Elements = elements.ToArray();
                Map.Props = props.ToArray();
            }
            else if (message is RoomResponse res)
            {
                TaskCompletionSource<RoomResponse> source;
                lock (_sources)
                {
                    _sources.TryGetValue(res.RequestId, out source);
                }

                if (null == source)
                {
                    Log.Warning($"Received a response for a request we are not tracking: {res.RequestId}.");
                }
                else
                {
                    source.SetResult(res);
                }
            }
            else
            {
                Log.Warning($"Received unhandled message: {message}.");
            }
        }
    }
}