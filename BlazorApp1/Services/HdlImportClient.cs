using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
namespace BlazorApp1.Services

{
    public class HdlImportClient
    {
        private readonly HttpClient httpClient;

        public HdlImportClient(HttpClient client)
        {
            httpClient = client;
        }

        private Task<HttpResponseMessage> SendRequestAsync(HdlSynthesisRequest request)
        {
            return httpClient.PostAsJsonAsync("/api/hdl/synthesize", request);
        }

        public async Task<HdlSynthesisResponse> SynthesizeAsync(HdlSynthesisRequest request)
        {
            using var response = await SendRequestAsync(request);

            HdlSynthesisResponse? result;

            try
            {
                result = await response.Content.ReadFromJsonAsync<HdlSynthesisResponse>();
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("The HDL service returned an invalid JSON response.");
            }

            if (result == null)
            {
                throw new InvalidOperationException("The HDL service returned no result.");
            }

            if (response.IsSuccessStatusCode == false)
            {
                string message = result.Diagnostics;

                if (string.IsNullOrWhiteSpace(message))
                {
                    message = $"The synthesis operation failed with status code: {response.StatusCode}";
                }

                throw new InvalidOperationException(message);
            }

            if (string.IsNullOrWhiteSpace(result.NetlistJson))
            {
                throw new InvalidOperationException("The service did not return a Netlist.");
            }

            return result;
        }
    }

    public class HdlSynthesisRequest
    {
        public string Source { get; set; } = string.Empty;
        public string TopModule { get; set; } = string.Empty;
    }

    public class HdlSynthesisResponse
    {
        public string NetlistJson { get; set; } = string.Empty;
        // متغير لازم مع العضو الثالث مالك api
        public string Diagnostics { get; set; } = string.Empty;
    }
}