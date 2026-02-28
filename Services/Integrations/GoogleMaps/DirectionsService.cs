using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Options;
using Microsoft.Extensions.Options;

namespace Services.Integrations.GoogleMaps
{
    public class GoogleDirectionsResult
    {
        public int[] WaypointOrder { get; set; } = Array.Empty<int>();
        public string? OverviewPolyline { get; set; }
        public double TotalDistanceMeters { get; set; }
        public double TotalDurationSeconds { get; set; }
        public string? Status { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IDirectionsService
    {
        Task<GoogleDirectionsResult?> GetOptimizedRouteAsync(
            string origin,
            string destination,
            IReadOnlyList<string> waypoints,
            string mode,
            CancellationToken ct = default);
    }

    public class GoogleDirectionsService : IDirectionsService
    {
        private readonly HttpClient _http;
        private readonly GoogleMapsOptions _opts;

        public GoogleDirectionsService(HttpClient http, IOptions<GoogleMapsOptions> opts)
        {
            _http = http;
            _opts = opts.Value;
        }

        public async Task<GoogleDirectionsResult?> GetOptimizedRouteAsync(
            string origin,
            string destination,
            IReadOnlyList<string> waypoints,
            string mode,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_opts.ApiKey))
                return null;

            if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination))
                return null;

            mode = string.IsNullOrWhiteSpace(mode) ? "driving" : mode.Trim().ToLowerInvariant();

            var originEnc = Uri.EscapeDataString(origin);
            var destEnc = Uri.EscapeDataString(destination);

            string waypointsPart = string.Empty;
            if (waypoints != null && waypoints.Count > 0)
            {
                // optimize:true|addr1|addr2|...
                var parts = new List<string>(waypoints.Count + 1) { "optimize:true" };
                foreach (var w in waypoints)
                {
                    if (string.IsNullOrWhiteSpace(w)) continue;
                    parts.Add(w);
                }
                waypointsPart = "&waypoints=" + Uri.EscapeDataString(string.Join("|", parts));
            }

            var url = $"{_opts.DirectionsBaseUrl}?origin={originEnc}&destination={destEnc}{waypointsPart}&mode={Uri.EscapeDataString(mode)}&key={Uri.EscapeDataString(_opts.ApiKey!)}";

            using var resp = await _http.GetAsync(url, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
                var errMsg = root.TryGetProperty("error_message", out var errEl) ? errEl.GetString() : null;

                var result = new GoogleDirectionsResult
                {
                    Status = status,
                    ErrorMessage = errMsg
                };

                if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                    return result;

                if (!root.TryGetProperty("routes", out var routesEl) || routesEl.ValueKind != JsonValueKind.Array || routesEl.GetArrayLength() == 0)
                    return result;

                var route = routesEl[0];

                if (route.TryGetProperty("overview_polyline", out var polyEl) && polyEl.TryGetProperty("points", out var pointsEl))
                    result.OverviewPolyline = pointsEl.GetString();

                if (route.TryGetProperty("waypoint_order", out var orderEl) && orderEl.ValueKind == JsonValueKind.Array)
                {
                    var tmp = new List<int>();
                    foreach (var it in orderEl.EnumerateArray())
                    {
                        if (it.ValueKind == JsonValueKind.Number && it.TryGetInt32(out var n))
                            tmp.Add(n);
                    }
                    result.WaypointOrder = tmp.ToArray();
                }

                // Sum legs
                if (route.TryGetProperty("legs", out var legsEl) && legsEl.ValueKind == JsonValueKind.Array)
                {
                    double dist = 0;
                    double dur = 0;
                    foreach (var leg in legsEl.EnumerateArray())
                    {
                        if (leg.TryGetProperty("distance", out var dEl) && dEl.TryGetProperty("value", out var dv) && dv.TryGetDouble(out var dm))
                            dist += dm;
                        if (leg.TryGetProperty("duration", out var tEl) && tEl.TryGetProperty("value", out var tv) && tv.TryGetDouble(out var ds))
                            dur += ds;
                    }
                    result.TotalDistanceMeters = dist;
                    result.TotalDurationSeconds = dur;
                }

                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}
