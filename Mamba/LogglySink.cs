using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Enklu.Mamba
{
    /// <summary>
    /// Quick and dirty implementation of a Loggly sink because the Nuget one
    /// doesn't want to cooperate.
    /// </summary>
    public class LogglySink : PeriodicBatchingSink
    {
        /// <summary>
        /// Makes requests.
        /// </summary>
        private static readonly HttpClient _client = new HttpClient();

        /// <summary>
        /// Loggly token.
        /// </summary>
        private readonly string _customerToken;

        /// <summary>
        /// Loggly tag.
        /// </summary>
        private readonly string _tag;

        /// <summary>
        /// Constructor.
        /// </summary>
        public LogglySink(string customerToken, string tag, int batchSizeLimit, TimeSpan period) : base(batchSizeLimit, period)
        {
            _customerToken = customerToken;
            _tag = tag;
        }
        
        /// <inheritdoc />
        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            await Task.WhenAll(events
                .Select(@event =>
                {
                    var list = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("level", @event.Level.ToString()),
                        new KeyValuePair<string, string>("message", @event.RenderMessage()),
                    };

                    foreach (var key in @event.Properties.Keys)
                    {
                        list.Add(new KeyValuePair<string, string>(key, @event.Properties[key].ToString()));
                    }

                    return _client
                        .PostAsync(
                            $"https://logs-01.loggly.com/inputs/{_customerToken}/tag/{_tag}",
                            new FormUrlEncodedContent(list));
                })
                .ToArray());
        }
    }
}