using System.Net;
using System.Net.Http.Json;

namespace BlazorApp1.Services
{
    // Sends circuit requests to CircuitApi.
    public sealed class CircuitApiClient
    {
        private readonly HttpClient _http;

        // Receives the API HttpClient.
        public CircuitApiClient(HttpClient http)
        {
            _http = http;
        }

        // Gets all saved circuits.
        public async Task<List<SavedCircuitDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await _http.GetFromJsonAsync<List<SavedCircuitDto>>(
                "api/circuits",
                cancellationToken) ?? new List<SavedCircuitDto>();
        }

        // Gets one circuit by its ID.
        public async Task<SavedCircuitDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            using var response = await _http.GetAsync(
                $"api/circuits/{id}",
                cancellationToken);

            // Returns null when the circuit does not exist.
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<SavedCircuitDto>(
                cancellationToken: cancellationToken);
        }

        // Saves a new circuit.
        public async Task<SavedCircuitDto> CreateAsync(
            string name,
            string jsonData,
            CancellationToken cancellationToken = default)
        {
            var request = new SaveCircuitApiRequest
            {
                Name = name,
                JsonData = jsonData
            };

            using var response = await _http.PostAsJsonAsync(
                "api/circuits",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<SavedCircuitDto>(
                       cancellationToken: cancellationToken)
                   ?? throw new InvalidOperationException(
                       "The API returned an empty circuit.");
        }

        // Updates a saved circuit.
        public async Task UpdateAsync(
            int id,
            string name,
            string jsonData,
            CancellationToken cancellationToken = default)
        {
            var request = new SaveCircuitApiRequest
            {
                Name = name,
                JsonData = jsonData
            };

            using var response = await _http.PutAsJsonAsync(
                $"api/circuits/{id}",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        // Deletes a saved circuit.
        public async Task DeleteAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            using var response = await _http.DeleteAsync(
                $"api/circuits/{id}",
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }

    // Holds the data sent when saving or updating a circuit.
    public sealed class SaveCircuitApiRequest
    {
        public string Name { get; set; } = string.Empty;
        public string JsonData { get; set; } = string.Empty;
    }

    // Holds a circuit returned by the API.
    public sealed class SavedCircuitDto
    {
        public int CircuitId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string JsonData { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}