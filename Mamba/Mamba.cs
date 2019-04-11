using System;
using System.Threading.Tasks;
using CommandLine;
using Enklu.Mamba.Network;
using Serilog;

namespace Enklu.Mamba
{
    public class Options
    {
        [Option('i', "ip", Default = "34.216.59.227", HelpText = "IP of Mycelium instance to connect to.")]
        public string MyceliumIp { get; set; }

        [Option('p', "port", Default = 10103, HelpText = "Port of Mycelium instance to connect to.")]
        public int MyceliumPort { get; set; }

        [Option('t', "token", Required = true, HelpText = "Login token issued from Trellis.")]
        public string LoginToken { get; set; }
    }

    class Mamba
    {
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

        private static async Task Run(Options options)
        {
            using (var controller = new MyceliumController(new MyceliumControllerConfiguration
            {
                Ip = options.MyceliumIp,
                Port = options.MyceliumPort,
                Token = options.LoginToken
            }))
            {
                controller.Start();

                Console.ReadLine();

                Log.Information("Shutting down.");
            }
        }
    }
}