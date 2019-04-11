using System;
using System.Net;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Enklu.Mamba.Util;
using Enklu.Mycerializer.Netty;
using Serilog;

namespace Enklu.Mamba.Network
{
    /// <summary>
    /// Controls Mycelium connection.
    /// </summary>
    public class MyceliumController : IDisposable
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

                    var handler = new MyceliumClientHandler(_config.Token);
                    handler.OnDisconnected += Handler_OnDisconnected;

                    pipeline.AddLast("handler", handler);
                }));

            Connect();
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
        /// <c>IDisposable</c> implementation.
        /// </summary>
        private void ReleaseUnmanagedResources()
        {
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