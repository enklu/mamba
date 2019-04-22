using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Enklu.Data;
using Newtonsoft.Json;
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
            
            var sceneId = await LoadApp();
            var elements = await LoadScene(sceneId);

            return elements;
        }

        private async Task<string> LoadApp()
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _config.TrellisToken);

            var url = $"{_config.TrellisUrl}/app/{_config.AppId}";

            Log.Information($"Starting app load: {url}", new { AppId = _config.AppId });

            var response = await _client.GetAsync(url);
            var str = await response.Content.ReadAsStringAsync();
            
            JObject result;
            try
            {
                result = JObject.Parse(str);
            }
            catch (Exception exception)
            {
                throw new Exception($"Could not deserialize get experience request: {str} -> {exception}");
            }

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var array = result["body"]["scenes"];

                return array[0].Value<string>();
            }
            
            Log.Information($"Nope: {response.StatusCode} : {response}");

            throw new Exception(result["error"].Value<string>());
        }

        private async Task<ElementData> LoadScene(string sceneId)
        {
            var url = $"{_config.TrellisUrl}/app/{_config.AppId}/scene/{sceneId}";

            Log.Information($"Starting scene load load: {url}", new { AppId = _config.AppId });

            var response = await _client.GetAsync(url);
            var str = await response.Content.ReadAsStringAsync();

            JObject result;
            try
            {
                result = JObject.Parse(str);
            }
            catch (Exception exception)
            {
                throw new Exception($"Could not deserialize get scene request: {str} -> {exception}");
            }

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var elements = result["body"]["elements"].Value<string>();

                return JsonConvert.DeserializeObject<ElementData>(elements);
            }

            Log.Information($"Nope: {response.StatusCode} : {response}");

            throw new Exception(result["error"].Value<string>());
        }

        public void Dispose()
        {
            //
        }
    }
}