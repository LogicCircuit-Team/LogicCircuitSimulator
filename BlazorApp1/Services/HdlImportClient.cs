using System.Net.Http.Json;
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

        private Task<HttpResponseMessage> SendRequestAsync(HdlSynthesisRequest request, CancellationToken cancellationToken)
        {
            return httpClient.PostAsJsonAsync("api/hdl/synthesize", request, cancellationToken);
        }

        public async Task<HdlSynthesisResponse> SynthesizeAsync(
            HdlSynthesisRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            try
            {
                using var response = await SendRequestAsync(request, cancellationToken);
                HdlSynthesisResponse? result;
                try
                {
                    result = await response.Content.ReadFromJsonAsync<HdlSynthesisResponse>(cancellationToken: cancellationToken);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException(
                        $"The HDL service returned HTTP {(int)response.StatusCode} ({response.StatusCode}) at {response.RequestMessage?.RequestUri} without the expected JSON response. Check ApiBaseUrl and make sure CircuitApi is running.", ex);
                }

                if (!response.IsSuccessStatusCode)
                {
                    string message = result?.Diagnostics ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(message))
                        message = $"The HDL request failed with HTTP {(int)response.StatusCode} ({response.StatusCode}).";
                    throw new InvalidOperationException(message);
                }
                if (result == null)
                    throw new InvalidOperationException("The HDL service returned no result.");
                if (string.IsNullOrWhiteSpace(result.NetlistJson))
                    throw new InvalidOperationException("The HDL service did not return a netlist.");
                return result;
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException("Unable to reach the HDL API. Start CircuitApi and check ApiBaseUrl, HTTPS and CORS settings.", ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException("The HDL request timed out. Check the API and Yosys configuration.", ex);
            }
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
        public string Diagnostics { get; set; } = string.Empty;
    }
}
