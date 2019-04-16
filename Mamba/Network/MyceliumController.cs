using System;
using System.Net;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Enklu.Data;
using Enklu.Mamba.Util;
using Enklu.Mycelium.Messages;
using Enklu.Mycelium.Messages.Experience;
using Enklu.Mycerializer.Netty;
using Serilog;

namespace Enklu.Mamba.Network
{
    /// <summary>
    /// Controls Mycelium connection.
    /// </summary>
    public class MyceliumController : IDisposable, IMyceliumInterface
    {
        /// <summary>
        /// Configuration for Mycelium connection.
        /// </summary>
        private readonly MyceliumControllerConfiguration _config;

        /// <summary>
        /// Creates channels.
        /// </summary>
        private Bootstrap _bootstrap;

        /// <summary>
        /// Event loop for processing channel events.
        /// </summary>
        private MultithreadEventLoopGroup _group;

        /// <summary>
        /// The IP address.
        /// </summary>
        private IPAddress _ip;

        /// <summary>
        /// The current handler.
        /// </summary>
        private MyceliumClientHandler _handler;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public MyceliumController(MyceliumControllerConfiguration config)
        {
            _config = config;
        }
        
        /// <summary>
        /// Starts the controller.
        /// </summary>
        public void Start()
        {
            var ipAddresses = Dns.GetHostAddresses(_config.Ip);
            if (0 == ipAddresses.Length)
            {
                throw new Exception("Could not resolve Mycelium host.");
            }

            _ip = ipAddresses[0];

            var metrics = new DummyNetworkMetricsProvider();
            var binder = new TypeBinder();

            _group = new MultithreadEventLoopGroup();
            _bootstrap = new Bootstrap();
            
            _bootstrap
                .Group(_group)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Handler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    var pipeline = channel.Pipeline;
                    pipeline.AddLast(
                        new PayloadEncoder(metrics, binder),
                        new PayloadDecoder(metrics, binder));

                    _handler = new MyceliumClientHandler(_config.Token);
                    _handler.OnDisconnected += Handler_OnDisconnected;

                    pipeline.AddLast("handler", _handler);
                }));

            Connect();
        }

        /// <inheritdoc />
        public void Create(string parentId, ElementData element)
        {
            try
            {
                _handler.Send(new CreateElementRequest
                {
                    Element = element,
                    ParentHash = _handler.Map.ElementHash(parentId)
                });
            }
            catch (NullReferenceException)
            {
                // handler may be null
            }
            catch (Exception ex)
            {
                Log.Warning($"Could not send create event: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void Update(ElementActionData[] actions)
        {
            foreach (var action in actions)
            {
                try
                {
                    _handler.Send(ToEvent(action));
                }
                catch (NullReferenceException)
                {
                    // handler may be null
                    return;
                }
                catch (Exception ex)
                {
                    Log.Warning($"Could not send update event: {ex.Message}");
                }
            }
        }
        
        /// <inheritdoc />
        public void Destroy(string id)
        {
            // TODO
        }

        /// <summary>
        /// Connects to Mycelium.
        /// </summary>
        private void Connect()
        {
            try
            {
                Log.Information($"Connecting to {_ip}:{_config.Port}...");

                _bootstrap
                    .ConnectAsync(_ip, _config.Port)
                    .Wait(TimeSpan.FromSeconds(3));
            }
            catch (Exception exception)
            {
                Log.Warning($"Could not connect to Mycelium: {exception.Message}.");

                // retry
                Connect();
            }
        }

        /// <summary>
        /// Called when the channel has been disconnected.
        /// </summary>
        /// <param name="handler">The handler.</param>
        private void Handler_OnDisconnected(MyceliumClientHandler handler)
        {
            Log.Information("Disconnected from Mycelium.");

            // reconnect
            Connect();
        }

        /// <summary>
        /// Creates an event from an action.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <returns></returns>
        private UpdateElementEvent ToEvent(ElementActionData action)
        {
            switch (action.SchemaType)
            {
                case ElementActionSchemaTypes.BOOL:
                {
                    return new UpdateElementBoolEvent
                    {
                        ElementHash = _handler.Map.ElementHash(action.ElementId),
                        PropHash = _handler.Map.PropHash(action.Key),
                        Value = (bool)action.Value
                    };
                }
                case ElementActionSchemaTypes.COL4:
                {
                    return new UpdateElementCol4Event
                    {
                        ElementHash = _handler.Map.ElementHash(action.ElementId),
                        PropHash = _handler.Map.PropHash(action.Key),
                        Value = (Col4)action.Value
                    };
                }
                case ElementActionSchemaTypes.FLOAT:
                {
                    return new UpdateElementFloatEvent
                    {
                        ElementHash = _handler.Map.ElementHash(action.ElementId),
                        PropHash = _handler.Map.PropHash(action.Key),
                        Value = (float)action.Value
                    };
                }
                case ElementActionSchemaTypes.INT:
                {
                    return new UpdateElementIntEvent
                    {
                        ElementHash = _handler.Map.ElementHash(action.ElementId),
                        PropHash = _handler.Map.PropHash(action.Key),
                        Value = (int)action.Value
                    };
                }
                case ElementActionSchemaTypes.STRING:
                {
                    return new UpdateElementStringEvent
                    {
                        ElementHash = _handler.Map.ElementHash(action.ElementId),
                        PropHash = _handler.Map.PropHash(action.Key),
                        Value = (string)action.Value
                    };
                }
                case ElementActionSchemaTypes.VEC3:
                {
                    return new UpdateElementVec3Event
                    {
                        ElementHash = _handler.Map.ElementHash(action.ElementId),
                        PropHash = _handler.Map.PropHash(action.Key),
                        Value = (Vec3)action.Value
                    };
                }
                default:
                {
                    throw new Exception(
                        $"Could not creaate update event for unknown schema type '{action.SchemaType}'.");
                }
            }
        }

        /// <summary>
        /// <c>IDisposable</c> implementation.
        /// </summary>
        private void ReleaseUnmanagedResources()
        {
            if (null == _group)
            {
                return;
            }

            _group
                .ShutdownGracefullyAsync(
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromSeconds(1))
                .Wait();
        }

        /// <summary>
        /// <c>IDisposable</c> implementation.
        /// </summary>
        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// <c>IDisposable</c> implementation.
        /// </summary>
        ~MyceliumController()
        {
            ReleaseUnmanagedResources();
        }
    }
}