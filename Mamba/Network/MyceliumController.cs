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
    public class MyceliumControllerConfiguration
    {
        public string Ip { get; set; }
        public int Port { get; set; }

        public string Token { get; set; }
    }

    public class MyceliumController : IDisposable
    {
        private readonly MyceliumControllerConfiguration _config;

        private Bootstrap _bootstrap;
        private MultithreadEventLoopGroup _group;
        private IPAddress _ip;

        public MyceliumController(MyceliumControllerConfiguration config)
        {
            _config = config;
        }

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

        private void Handler_OnDisconnected(MyceliumClientHandler obj)
        {
            Log.Information("Disconnected from Mycelium.");

            // reconnect
            //Connect();
        }

        private void ReleaseUnmanagedResources()
        {
            _group
                .ShutdownGracefullyAsync(
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromSeconds(1))
                .Wait();
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~MyceliumController()
        {
            ReleaseUnmanagedResources();
        }
    }
}