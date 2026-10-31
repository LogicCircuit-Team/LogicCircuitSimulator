using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BlazorApp1.Services
{
    public sealed class CircuitApiClient
    {
        private readonly HttpClient _http;
        private readonly SemaphoreSlim sessionLock = new(1, 1);
        private bool sessionReady;

        public CircuitApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<SavedCircuitDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await EnsureSessionAsync(cancellationToken);
            return await _http.GetFromJsonAsync<List<SavedCircuitDto>>("api/circuits", cancellationToken)
                   ?? new List<SavedCircuitDto>();
        }

        public async Task<SavedCircuitDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            await EnsureSessionAsync(cancellationToken);
            using var response = await _http.GetAsync($"api/circuits/{id}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            await EnsureSuccessfulAsync(response);
            return await response.Content.ReadFromJsonAsync<SavedCircuitDto>(cancellationToken: cancellationToken);
        }

        public async Task<SavedCircuitDto> CreateAsync(string name, string jsonData, CancellationToken cancellationToken = default)
        {
            await EnsureSessionAsync(cancellationToken);
            var request = new SaveCircuitApiRequest { Name = name, JsonData = jsonData };

            using var response = await _http.PostAsJsonAsync("api/circuits", request, cancellationToken);
            await EnsureSuccessfulAsync(response);

            return await response.Content.ReadFromJsonAsync<SavedCircuitDto>(cancellationToken: cancellationToken)
                   ?? throw new InvalidOperationException("The API returned an empty circuit.");
        }

        public async Task UpdateAsync(int id, string name, string jsonData, CancellationToken cancellationToken = default)
        {
            await EnsureSessionAsync(cancellationToken);
            var request = new SaveCircuitApiRequest { Name = name, JsonData = jsonData };

            using var response = await _http.PutAsJsonAsync($"api/circuits/{id}", request, cancellationToken);
            await EnsureSuccessfulAsync(response);
        }

        public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            await EnsureSessionAsync(cancellationToken);
            using var response = await _http.DeleteAsync($"api/circuits/{id}", cancellationToken);
            await EnsureSuccessfulAsync(response);
        }

        public async Task<List<SharedCircuitDto>> GetSharedAsync(string search, int page = 1, CancellationToken cancellationToken = default)
        {
            await EnsureSessionAsync(cancellationToken);
            return await _http.GetFromJsonAsync<List<SharedCircuitDto>>(
                $"api/community?search={Uri.EscapeDataString(search)}&page={page}", cancellationToken)
                ?? new List<SharedCircuitDto>();
        }

        public async Task<SharedCircuitDto> GetSharedByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            await EnsureSessionAsync(cancellationToken);
            return await _http.GetFromJsonAsync<SharedCircuitDto>($"api/community/{id}", cancellationToken)
                ?? throw new InvalidOperationException("The shared circuit was not found.");
        }

        public async Task<SharedCircuitDto> PublishAsync(string name, string authorName, string description, string jsonData)
        {
            await EnsureSessionAsync(CancellationToken.None);
            using var response = await _http.PostAsJsonAsync("api/community", new
            {
                name,
                authorName,
                description,
                jsonData
            });
            await EnsureSuccessfulAsync(response);
            return await response.Content.ReadFromJsonAsync<SharedCircuitDto>()
                ?? throw new InvalidOperationException("The API returned an empty circuit.");
        }

        public async Task UnpublishAsync(int id)
        {
            await EnsureSessionAsync(CancellationToken.None);
            using var response = await _http.DeleteAsync($"api/community/{id}");
            await EnsureSuccessfulAsync(response);
        }

        private async Task EnsureSessionAsync(CancellationToken cancellationToken)
        {
            if (sessionReady)
                return;
            await sessionLock.WaitAsync(cancellationToken);
            try
            {
                if (sessionReady)
                    return;
                using var response = await _http.GetAsync("api/session", cancellationToken);
                response.EnsureSuccessStatusCode();
                sessionReady = true;
            }
            finally
            {
                sessionLock.Release();
            }
        }

        private static async Task EnsureSuccessfulAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;
            string message = $"The service returned HTTP {(int)response.StatusCode}.";
            try
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (document.RootElement.TryGetProperty("message", out var value))
                    message = value.GetString() ?? message;
            }
            catch (JsonException)
            {
            }
            throw new InvalidOperationException(message);
        }
    }

    public sealed class SaveCircuitApiRequest
    {
        public string Name { get; set; } = string.Empty;
        public string JsonData { get; set; } = string.Empty;
    }

    public sealed class SavedCircuitDto
    {
        public int CircuitId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string JsonData { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class SharedCircuitDto
    {
        public int SharedCircuitId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JsonData { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public bool IsMine { get; set; }
    }
}