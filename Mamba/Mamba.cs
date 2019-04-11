using System;
using System.Threading.Tasks;
using CommandLine;
using Enklu.Mamba.Kinect;
using Enklu.Mamba.Network;
using Serilog;

namespace Enklu.Mamba
{
    /// <summary>
    /// Command line options.
    /// </summary>
    public class Options
    {
        [Option('i', "ip", Default = "34.216.59.227", HelpText = "IP of Mycelium instance to connect to.")]
        public string MyceliumIp { get; set; }

        [Option('p', "port", Default = 10103, HelpText = "Port of Mycelium instance to connect to.")]
        public int MyceliumPort { get; set; }

        [Option('t', "token", Required = true, HelpText = "Login token issued from Trellis.")]
        public string LoginToken { get; set; }
    }

    /// <summary>
    /// Entry point.
    /// </summary>
    class Mamba
    {
        /// <summary>
        /// Main.
        /// </summary>
        static void Main(params string[] argv)
        {
            // logging
            var log = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .MinimumLevel.Information()
                .CreateLogger();
            Log.Logger = log;
            Log.Information("Logging initialized.");

            // parse
            Parser
                .Default
                .ParseArguments<Options>(argv)
                .WithParsed(o => Run(o).Wait());
        }

        /// <summary>
        /// Starts the application.
        /// </summary>
        /// <param name="options">Options to run with.</param>
        /// <returns></returns>
        private static async Task Run(Options options)
        {
            using (var network = new MyceliumController(new MyceliumControllerConfiguration
            {
                Ip = options.MyceliumIp,
                Port = options.MyceliumPort,
                Token = options.LoginToken
            }))
            {
                using (var kinect = new KinectController(new KinectControllerConfiguration(), network))
                {
                    network.Start();
                    kinect.Start();

                    Console.ReadLine();

                    Log.Information("Shutting down.");
                }
            }
        }
    }
}