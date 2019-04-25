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
    /// <summary>
    /// Controls loading an experience.
    /// </summary>
    public class ExperienceController : IDisposable
    {
        /// <summary>
        /// Makes requests.
        /// </summary>
        private static readonly HttpClient _Client = new HttpClient();

        /// <summary>
        /// Configuration object.
        /// </summary>
        private readonly ExperienceControllerConfig _config;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="config">Configuration object.</param>
        public ExperienceController(ExperienceControllerConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Starts the controller, pulling down the data for a scene.
        /// </summary>
        /// <returns></returns>
        public async Task<ElementData> Initialize()
        {
            Log.Information($"Loading experience '{_config.AppId}'.");
            
            var sceneId = await LoadApp();
            var elements = await LoadScene(sceneId);

            return elements;
        }

        /// <summary>
        /// Loads the scene id for an app.
        /// </summary>
        private async Task<string> LoadApp()
        {
            _Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _config.TrellisToken);

            var url = $"{_config.TrellisUrl}/app/{_config.AppId}";

            Log.Information($"Starting app load: {url}", new { AppId = _config.AppId });

            var response = await _Client.GetAsync(url);
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

        /// <summary>
        /// Loads the scene data for a specific scene.
        /// </summary>
        /// <param name="sceneId">The scene.</param>
        /// <returns></returns>
        private async Task<ElementData> LoadScene(string sceneId)
        {
            var url = $"{_config.TrellisUrl}/app/{_config.AppId}/scene/{sceneId}";

            Log.Information($"Starting scene load load: {url}", new { AppId = _config.AppId });

            var response = await _Client.GetAsync(url);
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

            var error = result["error"];
            var errorStr = error != null ? error.Value<string>() : "Unknown error";
            throw new Exception(errorStr);
        }

        /// <summary>
        /// Disposes of any resources.
        /// </summary>
        public void Dispose()
        {
            // TODO
        }
    }
}