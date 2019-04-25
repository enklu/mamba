using System;
using System.IO;
using System.Threading.Tasks;
using Enklu.Data;
using Enklu.Mamba.Kinect;
using Enklu.Mamba.Network;
using Mamba.Experience;
using Newtonsoft.Json;
using Serilog;

namespace Enklu.Mamba
{
    /// <summary>
    /// Entry point.
    /// </summary>
    class Mamba
    {
        /// <summary>
        /// Main.
        /// </summary>
        static void Main()
        {
            // logging
            var log = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .MinimumLevel.Information()
                .CreateLogger();
            Log.Logger = log;
            Log.Information("Logging initialized.");
            
            var config = Configuration();

            Log.Information("Configuration: {0}", config);
            Run(config).Wait();
        }

        /// <summary>
        /// Starts the application.
        /// </summary>
        /// <param name="config">Options to run with.</param>
        /// <returns></returns>
        private static async Task Run(MambaConfiguration config)
        {
            using (var experience = new ExperienceController(new ExperienceControllerConfig
            {
                AppId = config.ExperienceId,
                TrellisToken = config.Token,
                TrellisUrl = config.TrellisUrl
            }))
            {
                ElementData elements;
                // load experience first
                try
                {
                    elements = await experience.Initialize();
                }
                catch (Exception exception)
                {
                    Log.Error($"Could not initialize experience: '{exception}'.");

                    return;
                }

                using (var network = new MyceliumController(new MyceliumControllerConfiguration
                {
                    Ip = config.MyceliumIp,
                    Port = config.MyceliumPort,
                    Token = config.Token
                }))
                {
                    using (var kinect = new KinectController(new KinectControllerConfiguration(), network, elements))
                    {
                        network.Start();
                        kinect.Start();

                        Console.ReadLine();

                        Log.Information("Shutting down.");
                    }
                }
            }
        }

        /// <summary>
        /// Creates an AppActorConfiguration.
        /// </summary>
        /// <returns></returns>
        private static MambaConfiguration Configuration()
        {
            // construct the application config
            var config = new MambaConfiguration();

            // override defaults with app-config.json
            var src = File.ReadAllText("app-config.json");
            config.Override(JsonConvert.DeserializeObject<MambaConfiguration>(src));

            // override with environment variables
            SetFromEnvironment("EXPERIENCE_ID", ref config.ExperienceId, a => a);
            SetFromEnvironment("TRELLIS_URL", ref config.TrellisUrl, a => a);
            SetFromEnvironment("TRELLIS_TOKEN", ref config.Token, a => a);
            SetFromEnvironment("MYCELIUM_IP", ref config.MyceliumIp, a => a);
            SetFromEnvironment("MYCELIUM_PORT", ref config.MyceliumPort, int.Parse);
            SetFromEnvironment("GRAPHITE_HOST", ref config.GraphiteHost, a => a);
            SetFromEnvironment("GRAPHITE_KEY", ref config.GraphiteKey, a => a);
            
            return config;
        }

        /// <summary>
        /// Sets a value from an environment variable.
        /// </summary>
        /// <typeparam name="T">The type of prop to set.</typeparam>
        /// <param name="name">The name of the environment variable.</param>
        /// <param name="prop">A reference to the field.<param>
        /// <param name="converter">A function that converts from string to the required type.</param>
        private static void SetFromEnvironment<T>(string name, ref T prop, Func<string, T> converter)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value))
            {
                prop = converter(value);
            }
        }
    }
}