using System;
using System.Net;
using System.Threading.Tasks;
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
    /// <inheritdoc cref="IDisposable" />
    /// <inheritdoc cref="IMyceliumInterface" />
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
        public async Task<ElementData> Create(
            string parentId,
            ElementData element,
            string owner = null,
            ElementExpirationType expiration = ElementExpirationType.Session)
        {
            var parentHash = _handler.Map.ElementHash(parentId);
            if (0 == parentHash)
            {
                throw new Exception($"Could not find parent hash for %{parentId}.");
            }

            OverwriteIds(element);
            
            Log.Information($"Creating element with parent [Id: ${parentId}, Hash: ${parentHash}");

            var response = await _handler.SendRequest(new CreateElementRequest
            {
                ParentHash = _handler.Map.ElementHash(parentId),
                Element = element,
                Owner = owner,
                Expiration = expiration
            });

            if (response.Success)
            {
                return element;
            }

            throw new Exception("Could not create element.");
        }

        /// <inheritdoc />
        public void Update(ElementActionData[] actions)
        {
            foreach (var action in actions)
            {
                var elementHash = _handler.Map.ElementHash(action.ElementId);
                var propHash = _handler.Map.PropHash(action.Key);

                if (0 == elementHash)
                {
                    Log.Warning($"Could not find element hash for ${action.ElementId}.");
                    return;
                }

                if (0 == propHash)
                {
                    Log.Warning($"Could not find prop hash for ${action.Key}.");
                    return;
                }

                try
                {
                    _handler.Send(ToEvent(elementHash, propHash, action));
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
        public async Task Destroy(string id)
        {
            var response = await _handler.SendRequest(new DeleteElementRequest
            {
                ElementHash = _handler.Map.ElementHash(id)
            });

            if (response.Success)
            {
                return;
            }

            throw new Exception("Could not create element.");
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
        /// <param name="elementHash">The id of the element.</param>
        /// <param name="propHash">The id of the hash.</param>
        /// <param name="action">The action.</param>
        /// <returns></returns>
        private static UpdateElementEvent ToEvent(ushort elementHash, ushort propHash, ElementActionData action)
        {
            switch (action.SchemaType)
            {
                case ElementActionSchemaTypes.BOOL:
                {
                    return new UpdateElementBoolEvent
                    {
                        ElementHash = elementHash,
                        PropHash = propHash,
                        Value = (bool) action.Value
                    };
                }
                case ElementActionSchemaTypes.COL4:
                {
                    return new UpdateElementCol4Event
                    {
                        ElementHash = elementHash,
                        PropHash = propHash,
                        Value = (Col4) action.Value
                    };
                }
                case ElementActionSchemaTypes.FLOAT:
                {
                    return new UpdateElementFloatEvent
                    {
                        ElementHash = elementHash,
                        PropHash = propHash,
                        Value = (float) action.Value
                    };
                }
                case ElementActionSchemaTypes.INT:
                {
                    return new UpdateElementIntEvent
                    {
                        ElementHash = elementHash,
                        PropHash = propHash,
                        Value = (int) action.Value
                    };
                }
                case ElementActionSchemaTypes.STRING:
                {
                    return new UpdateElementStringEvent
                    {
                        ElementHash = elementHash,
                        PropHash = propHash,
                        Value = (string) action.Value
                    };
                }
                case ElementActionSchemaTypes.VEC3:
                {
                    return new UpdateElementVec3Event
                    {
                        ElementHash = elementHash,
                        PropHash = propHash,
                        Value = (Vec3) action.Value
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
        /// Updates ids.
        /// </summary>
        /// <param name="data">Ids.</param>
        private static void OverwriteIds(ElementData data)
        {
            data.Id = Guid.NewGuid().ToString();

            foreach (var child in data.Children)
            {
                OverwriteIds(child);
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

        /// <inheritdoc />
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