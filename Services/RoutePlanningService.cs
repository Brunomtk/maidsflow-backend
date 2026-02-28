using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.DTO.RoutePlanning;
using Core.Exceptions;
using Infrastructure.Repositories;
using Services.Integrations.GoogleMaps;
using Services.Security;

namespace Services
{
    public interface IRoutePlanningService
    {
        Task<RoutePlanResponseDTO> BuildOptimizedDayRouteAsync(int professionalId, RoutePlanRequestDTO request, CancellationToken ct = default);
    }

    public class RoutePlanningService : IRoutePlanningService
    {
        private readonly IUnitOfWork _uow;
        private readonly IScopeGuard _scope;
        private readonly ICurrentUser _currentUser;
        private readonly IDirectionsService _directions;
        private readonly IGeocodingService _geocoding;

        public RoutePlanningService(IUnitOfWork uow, IScopeGuard scope, ICurrentUser currentUser, IDirectionsService directions, IGeocodingService geocoding)
        {
            _uow = uow;
            _scope = scope;
            _currentUser = currentUser;
            _directions = directions;
            _geocoding = geocoding;
        }

        public async Task<RoutePlanResponseDTO> BuildOptimizedDayRouteAsync(int professionalId, RoutePlanRequestDTO request, CancellationToken ct = default)
        {
            await _scope.EnsureProfessionalAccessAsync(professionalId);

            if (string.IsNullOrWhiteSpace(request.Date))
                throw new BadRequestException("Date is required (yyyy-MM-dd).");

            if (!DateOnly.TryParseExact(request.Date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                throw new BadRequestException("Invalid Date. Use yyyy-MM-dd.");

            var tzId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "America/Los_Angeles" : request.TimeZoneId.Trim();

            // We store appointment times as provided (no tz conversion). So we filter by local date boundaries.
            var startLocal = date.ToDateTime(TimeOnly.MinValue);
            var endLocal = date.AddDays(1).ToDateTime(TimeOnly.MinValue);

            var all = await _uow.Appointments.GetAppointmentsByProfessionalAsync(professionalId);
            var day = all
                .Where(a => a.Start >= startLocal && a.Start < endLocal)
                .OrderBy(a => a.Start)
                .ToList();

            // Only scheduled-ish appointments with address
            var stops = new List<(int AppointmentId, string Title, string Address, DateTime Start, DateTime End)>();

            foreach (var a in day)
            {
                var addr = (a.Address ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(addr) && a.CustomerAddressId.HasValue)
                {
                    var ca = await _uow.CustomerAddresses.GetByIdAsync(a.CustomerAddressId.Value);
                    if (ca != null)
                    {
                        addr = $"{ca.AddressLine1}, {ca.City}, {ca.State} {ca.ZipCode}".Trim().Trim(',');
                    }
                }

                if (string.IsNullOrWhiteSpace(addr))
                    continue;

                stops.Add((a.Id, a.Title ?? string.Empty, addr, a.Start, a.End));
            }

            if (stops.Count == 0)
            {
                return new RoutePlanResponseDTO
                {
                    Date = request.Date.Trim(),
                    TimeZoneId = tzId,
                    Origin = request.StartAddress ?? string.Empty,
                    Destination = request.EndAddress ?? string.Empty,
                    Stops = new List<RoutePlanStopDTO>()
                };
            }

            // Default origin/destination from first/last appointment
            var origin = string.IsNullOrWhiteSpace(request.StartAddress) ? stops.First().Address : request.StartAddress!.Trim();
            var destination = string.IsNullOrWhiteSpace(request.EndAddress) ? stops.Last().Address : request.EndAddress!.Trim();

            // Build waypoints: middle stops by default, but if custom origin/destination, include all appointment stops.
            List<(int AppointmentId, string Title, string Address, DateTime Start, DateTime End)> waypointStops;
            if (string.IsNullOrWhiteSpace(request.StartAddress) && string.IsNullOrWhiteSpace(request.EndAddress))
            {
                waypointStops = stops.Skip(1).Take(Math.Max(0, stops.Count - 2)).ToList();
            }
            else
            {
                waypointStops = stops.ToList();
            }

            var waypointAddresses = waypointStops.Select(s => s.Address).ToList();

            var dir = await _directions.GetOptimizedRouteAsync(origin, destination, waypointAddresses, request.Mode, ct);

            // If Google didn't return OK, fall back to chronological order.
            var orderedStops = new List<(int AppointmentId, string Title, string Address, DateTime Start, DateTime End)>();

            if (dir == null || !string.Equals(dir.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                orderedStops = stops;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.StartAddress) && string.IsNullOrWhiteSpace(request.EndAddress))
                {
                    // origin = first stop, destination = last stop, waypoint_order indexes into middle stops.
                    orderedStops.Add(stops.First());

                    if (waypointStops.Count > 0)
                    {
                        foreach (var idx in dir.WaypointOrder)
                        {
                            if (idx >= 0 && idx < waypointStops.Count)
                                orderedStops.Add(waypointStops[idx]);
                        }
                    }

                    if (stops.Count > 1)
                        orderedStops.Add(stops.Last());
                }
                else
                {
                    // Custom origin/destination: waypoint_order indexes into ALL appointment stops.
                    if (waypointStops.Count > 0)
                    {
                        foreach (var idx in dir.WaypointOrder)
                        {
                            if (idx >= 0 && idx < waypointStops.Count)
                                orderedStops.Add(waypointStops[idx]);
                        }
                    }
                }
            }

            var resp = new RoutePlanResponseDTO
            {
                Date = request.Date.Trim(),
                TimeZoneId = tzId,
                Origin = origin,
                Destination = destination,
                OverviewPolyline = dir?.OverviewPolyline,
                TotalDistanceKm = Math.Round(((dir?.TotalDistanceMeters ?? 0) / 1000.0), 2),
                TotalDurationMinutes = (int)Math.Round(((dir?.TotalDurationSeconds ?? 0) / 60.0), 0),
                Stops = new List<RoutePlanStopDTO>()
            };

            
            // Populate coordinates for stops (so the frontend can place pins and focus reliably).
            foreach (var st in orderedStops)
            {
                double? lat = null;
                double? lng = null;

                var geo = await _geocoding.GeocodeAsync(st.Address, ct);
                if (geo != null)
                {
                    lat = geo.Latitude;
                    lng = geo.Longitude;
                }

                resp.Stops.Add(new RoutePlanStopDTO
                {
                    AppointmentId = st.AppointmentId,
                    Title = st.Title,
                    Address = st.Address,
                    Latitude = lat,
                    Longitude = lng,
                    Start = st.Start,
                    End = st.End
                });
            }

return resp;
        }
    }
}
