using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Enklu.Data;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Mamba.Experience
{
    public class ExperienceControllerConfig
    {
        public string AppId;

        public string TrellisUrl;
        public string TrellisToken;
    }

    public class ExperienceController : IDisposable
    {
        /// <summary>
        /// Message from http requests.
        /// </summary>
        private class HttpResponse
        {
            /// <summary>
            /// True iff successful.
            /// </summary>
            public bool Success { get; set; }

            /// <summary>
            /// The error, if <c>Success</c> is false.
            /// </summary>
            public string Error { get; set; }

            /// <summary>
            /// Value to pass along.
            /// </summary>
            public object Value { get; set; }
        }

        private static readonly HttpClient _client = new HttpClient();
        private readonly ExperienceControllerConfig _config;

        public ExperienceController(ExperienceControllerConfig config)
        {
            _config = config;
        }

        public async Task<ElementData> Initialize()
        {
            Log.Information($"Loading experience '{_config.AppId}'.");

            try
            {
                var result = await LoadApp()
                    .ContinueWith(ReceiveAppData)
                    .ContinueWith(LoadScene);

                return await result;
            }
            catch (HttpRequestException exception)
            {
                Log.Error(
                    $"Could not load experience: {exception}.",
                    new { _config.AppId });

                throw new Exception("Could not load.");
            }
        }
        
        private Task<HttpResponseMessage> LoadApp()
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _config.TrellisToken);

            var url = $"{_config.TrellisUrl}app/{_config.AppId}";

            Log.Information($"Starting app load: {url}", new { AppId = _config.AppId });

            return _client.GetAsync(url);
        }

        private string ReceiveAppData(Task<HttpResponseMessage> req)
        {
            var response = req.Result;

            var stream = response.Content.ReadAsStringAsync();
            try
            {
                stream.Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
                throw new Exception("Request timed out.");
            }

            JObject result;
            try
            {
                result = JObject.Parse(stream.Result);
            }
            catch (Exception exception)
            {
                throw new Exception($"Could not deserialize get experience request: {exception} -> {stream.Result}");
            }

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var array = result["body"]["scenes"];

                return array[0].Value<string>();
            }

            throw new Exception(result["error"].Value<string>());
        }

        private Task<ElementData> LoadScene(Task<string> task)
        {
            throw new Exception("not implemented");
        }

        public void Dispose()
        {
            //
        }
    }
}