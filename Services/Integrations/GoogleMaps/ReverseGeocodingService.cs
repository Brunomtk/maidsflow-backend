using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Options;
using Microsoft.Extensions.Options;

namespace Services.Integrations.GoogleMaps
{
    public interface IReverseGeocodingService
    {
        Task<string?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default);
    }

    public class GoogleReverseGeocodingService : IReverseGeocodingService
    {
        private readonly HttpClient _http;
        private readonly GoogleMapsOptions _opts;

        public GoogleReverseGeocodingService(HttpClient http, IOptions<GoogleMapsOptions> opts)
        {
            _http = http;
            _opts = opts.Value;
        }

        public async Task<string?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_opts.ApiKey)) return null;

            var url = $"{_opts.GeocodingBaseUrl}?latlng={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&key={Uri.EscapeDataString(_opts.ApiKey)}";

            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("status", out var statusEl)) return null;
                var status = statusEl.GetString();
                if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase)) return null;

                if (!doc.RootElement.TryGetProperty("results", out var resultsEl)) return null;
                if (resultsEl.ValueKind != JsonValueKind.Array || resultsEl.GetArrayLength() == 0) return null;

                var first = resultsEl[0];
                if (first.TryGetProperty("formatted_address", out var addrEl))
                    return addrEl.GetString();
            }
            catch
            {
                return null;
            }

            return null;
        }
    }
}
