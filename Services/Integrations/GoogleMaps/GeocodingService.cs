using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Services.Integrations.GoogleMaps
{
    public record GeocodeResult(double Latitude, double Longitude, string? FormattedAddress);

    public interface IGeocodingService
    {
        Task<GeocodeResult?> GeocodeAsync(string address, CancellationToken ct = default);
    }

    public class GoogleGeocodingService : IGeocodingService
    {
        private readonly HttpClient _http;
        private readonly GoogleMapsOptions _opts;
        private readonly IMemoryCache _cache;

        public GoogleGeocodingService(HttpClient http, IOptions<GoogleMapsOptions> opts, IMemoryCache cache)
        {
            _http = http;
            _opts = opts.Value;
            _cache = cache;
        }

        public async Task<GeocodeResult?> GeocodeAsync(string address, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_opts.ApiKey)) return null;
            address = address.Trim();
            if (address.Length < 6) return null;

            var cacheKey = "gmaps:geocode:" + address.ToLowerInvariant();
            if (_cache.TryGetValue(cacheKey, out GeocodeResult cached))
                return cached;

            var url = $"{_opts.GeocodingBaseUrl}?address={Uri.EscapeDataString(address)}&key={Uri.EscapeDataString(_opts.ApiKey!)}";

            using var resp = await _http.GetAsync(url, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
                if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                    return null;

                if (!root.TryGetProperty("results", out var resultsEl) || resultsEl.ValueKind != JsonValueKind.Array || resultsEl.GetArrayLength() == 0)
                    return null;

                var first = resultsEl[0];

                var formatted = first.TryGetProperty("formatted_address", out var fa) ? fa.GetString() : null;

                if (!first.TryGetProperty("geometry", out var geom) || !geom.TryGetProperty("location", out var loc))
                    return null;

                if (!loc.TryGetProperty("lat", out var latEl) || !loc.TryGetProperty("lng", out var lngEl))
                    return null;

                var lat = latEl.GetDouble();
                var lng = lngEl.GetDouble();

                // basic validation
                if (Math.Abs(lat) > 90 || Math.Abs(lng) > 180) return null;
                if (lat == 0 && lng == 0) return null;

                var result = new GeocodeResult(lat, lng, formatted);
                _cache.Set(cacheKey, result, TimeSpan.FromDays(30));
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}
