using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.GpsTracking;
using Core.Enums.GpsTracking;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using Services.Security;
using Core.Exceptions;
using Core.Options;
using Microsoft.Extensions.Options;
using Services.Integrations.GoogleMaps;

namespace Services
{
    public interface IGpsTrackingService
    {
        Task<PagedResult<GpsTracking>> GetPagedAsync(GpsTrackingFiltersDTO filters);
        Task<GpsTracking?> GetByIdAsync(int id);
        Task<GpsTracking> CreateAsync(CreateGpsTrackingDTO dto);
        Task<GpsTracking?> UpdateAsync(int id, UpdateGpsTrackingDTO dto);
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Retorna as rotas (pontos + resumo) de um profissional em um intervalo de dias.
        /// A data é interpretada no fuso informado (por padrão: America/Sao_Paulo).
        /// </summary>
        Task<List<GpsRouteDayDTO>> GetProfessionalRoutesAsync(
            int professionalId,
            DateOnly? dateFrom,
            DateOnly? dateTo,
            string? timeZoneId = null,
            bool includePoints = true,
            bool includeStops = true);
    }

    public class GpsTrackingService : IGpsTrackingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;
        private readonly GpsTrackingOptions _opts;
        private readonly IReverseGeocodingService _reverseGeocoding;

        public GpsTrackingService(
            IUnitOfWork unitOfWork,
            ICurrentUser currentUser,
            IScopeGuard scope,
            IOptions<GpsTrackingOptions> opts,
            IReverseGeocodingService reverseGeocoding)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
            _opts = opts.Value;
            _reverseGeocoding = reverseGeocoding;
        }

        public async Task<PagedResult<GpsTracking>> GetPagedAsync(GpsTrackingFiltersDTO filters)
{
    if (!_currentUser.IsAdmin)
    {
        var companyId = await _scope.GetScopedCompanyIdAsync();
        if (companyId.HasValue) filters.CompanyId = companyId.Value;

        if (_currentUser.IsProfessional)
        {
            var pid = await _scope.GetScopedProfessionalIdAsync();
            if (pid.HasValue) filters.ProfessionalId = pid.Value;
        }
    }

    return await _unitOfWork.GpsTrackings.GetPagedAsync(filters);
}


        public async Task<GpsTracking?> GetByIdAsync(int id)
{
    var model = await _unitOfWork.GpsTrackings.GetByIdAsync(id);
    if (model == null) return null;

    if (!_currentUser.IsAdmin)
    {
        await _scope.EnsureCompanyAccessAsync(model.CompanyId);

        if (_currentUser.IsProfessional)
        {
            var pid = await _scope.GetScopedProfessionalIdAsync();
            if (!pid.HasValue || pid.Value != model.ProfessionalId)
                throw new ForbiddenException("Você não tem permissão para acessar este GPS Tracking.");
        }
    }

    return model;
}


        public async Task<GpsTracking> CreateAsync(CreateGpsTrackingDTO dto)
{
    if (!_currentUser.IsAdmin)
    {
        var companyId = await _scope.GetScopedCompanyIdAsync();
        if (companyId.HasValue) dto.CompanyId = companyId.Value;

        if (_currentUser.IsProfessional)
        {
            var pid = await _scope.GetScopedProfessionalIdAsync();
            if (!pid.HasValue) throw new ForbiddenException("Escopo de profissional inválido.");
            dto.ProfessionalId = pid.Value;
        }

        // garante que profissional pertence à company
        await _scope.EnsureProfessionalInCompanyAsync(dto.ProfessionalId);
    }

            var nowUtc = DateTime.UtcNow;

            // valida latitude/longitude
            var lat = dto.Latitude ?? 0;
            var lng = dto.Longitude ?? 0;
            if (!IsValidLatLng(lat, lng))
                throw new BadRequestException("Latitude/Longitude inválidos.");

            // valida timestamp
            var ts = dto.Timestamp ?? nowUtc;
            var maxFuture = nowUtc.AddMinutes(Math.Max(1, _opts.MaxFutureMinutes));
            if (ts > maxFuture)
                throw new BadRequestException("Timestamp inválido (muito no futuro).");

            var maxPast = nowUtc.AddHours(-Math.Max(1, _opts.MaxPastHours));
            if (ts < maxPast)
                throw new BadRequestException("Timestamp inválido (muito antigo).");

            // dedup: ignora pontos repetidos/ruins (melhora performance e rotas)
            var last = await _unitOfWork.GpsTrackings.GetLastPointAsync(dto.ProfessionalId, dto.CompanyId);
            if (last != null && last.Location != null)
            {
                var lastTs = DateTime.SpecifyKind(last.Timestamp, DateTimeKind.Utc);
                var window = TimeSpan.FromSeconds(Math.Max(1, _opts.DedupWindowSeconds));
                if (ts - lastTs <= window)
                {
                    var dist = HaversineMeters(last.Location.Latitude, last.Location.Longitude, lat, lng);
                    if (dist <= Math.Max(0.1, _opts.DedupDistanceMeters))
                        return last; // retorna o último para evitar flood
                }
            }

            // reverse geocoding (opcional): se não veio address, tenta preencher
            var address = dto.Address;
            if (_opts.EnableReverseGeocoding && string.IsNullOrWhiteSpace(address))
            {
                address = await _reverseGeocoding.ReverseGeocodeAsync(lat, lng);
            }

            var model = new GpsTracking
            {
                ProfessionalId = dto.ProfessionalId,
                ProfessionalName = dto.ProfessionalName,
                CompanyId = dto.CompanyId,
                CompanyName = dto.CompanyName,
                TeamId = dto.TeamId,
                Location = new Location
                {
                    Latitude = lat,
                    Longitude = lng,
                    Address = address ?? string.Empty,
                },
                Status = dto.Status ?? GpsTrackingStatus.Active,
                Source = dto.Source ?? GpsTrackingSource.Gps,
                AppointmentId = dto.AppointmentId,
                CustomerId = dto.CustomerId,
                CheckRecordId = dto.CheckRecordId,
                Notes = dto.Notes,
                Timestamp = ts,
                CreatedDate = nowUtc,
                UpdatedDate = nowUtc
            };

            await _unitOfWork.GpsTrackings.Add(model);
            await _unitOfWork.SaveAsync();
            return model;
        }

        public async Task<GpsTracking?> UpdateAsync(int id, UpdateGpsTrackingDTO dto)
        {
            var model = await _unitOfWork.GpsTrackings.GetByIdAsync(id);
            if (model == null) return null;

            if (!_currentUser.IsAdmin)
            {
                await _scope.EnsureCompanyAccessAsync(model.CompanyId);

                if (_currentUser.IsProfessional)
                {
                    var pid = await _scope.GetScopedProfessionalIdAsync();
                    if (!pid.HasValue || pid.Value != model.ProfessionalId)
                        throw new ForbiddenException("Você não tem permissão para editar este GPS Tracking.");
                }
            }

            // ProfessionalId/CompanyId não alteráveis aqui (só admin)
            if (_currentUser.IsAdmin && dto.ProfessionalId.HasValue)
                model.ProfessionalId = dto.ProfessionalId.Value;

            if (!string.IsNullOrWhiteSpace(dto.ProfessionalName))
                model.ProfessionalName = dto.ProfessionalName;

            if (_currentUser.IsAdmin && dto.CompanyId.HasValue)
                model.CompanyId = dto.CompanyId.Value;

            if (!string.IsNullOrWhiteSpace(dto.CompanyName))
                model.CompanyName = dto.CompanyName;

            if (dto.TeamId.HasValue)
                model.TeamId = dto.TeamId;            if (dto.Latitude.HasValue)
                model.Location.Latitude = dto.Latitude.Value;
            if (dto.Longitude.HasValue)
                model.Location.Longitude = dto.Longitude.Value;
            if (!string.IsNullOrWhiteSpace(dto.Address))
                model.Location.Address = dto.Address;if (dto.Status.HasValue)
                model.Status = dto.Status.Value;

            if (dto.Source.HasValue)
                model.Source = dto.Source.Value;

            if (_currentUser.IsAdmin)
            {
                if (dto.AppointmentId.HasValue) model.AppointmentId = dto.AppointmentId.Value;
                if (dto.CustomerId.HasValue) model.CustomerId = dto.CustomerId.Value;
                if (dto.CheckRecordId.HasValue) model.CheckRecordId = dto.CheckRecordId.Value;
            }

            if (dto.Notes != null)
                model.Notes = dto.Notes;

            if (dto.Timestamp.HasValue)
                model.Timestamp = dto.Timestamp.Value;

            model.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.GpsTrackings.Update(model);
            await _unitOfWork.SaveAsync();
            return model;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var model = await _unitOfWork.GpsTrackings.GetByIdAsync(id);
            if (model == null) return false;

            if (!_currentUser.IsAdmin)
            {
                await _scope.EnsureCompanyAccessAsync(model.CompanyId);

                if (_currentUser.IsProfessional)
                {
                    var pid = await _scope.GetScopedProfessionalIdAsync();
                    if (!pid.HasValue || pid.Value != model.ProfessionalId)
                        throw new ForbiddenException("Você não tem permissão para excluir este GPS Tracking.");
                }
            }

            _unitOfWork.GpsTrackings.Delete(model);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<List<GpsRouteDayDTO>> GetProfessionalRoutesAsync(
            int professionalId,
            DateOnly? dateFrom,
            DateOnly? dateTo,
            string? timeZoneId = null,
            bool includePoints = true,
            bool includeStops = true)
        {
            // Segurança / escopo
            await _scope.EnsureProfessionalAccessAsync(professionalId);

            var tz = ResolveTimeZone(timeZoneId);

            // Se não vier intervalo, assume o dia atual no fuso
            if (!dateFrom.HasValue && !dateTo.HasValue)
            {
                var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
                dateFrom = DateOnly.FromDateTime(todayLocal);
                dateTo = dateFrom;
            }

            if (dateFrom.HasValue && !dateTo.HasValue) dateTo = dateFrom;
            if (!dateFrom.HasValue && dateTo.HasValue) dateFrom = dateTo;

            // normaliza ordem
            if (dateFrom!.Value > dateTo!.Value)
            {
                (dateFrom, dateTo) = (dateTo, dateFrom);
            }

            var localFrom = new DateTime(dateFrom.Value.Year, dateFrom.Value.Month, dateFrom.Value.Day, 0, 0, 0, DateTimeKind.Unspecified);
            var localToExclusive = new DateTime(dateTo.Value.AddDays(1).Year, dateTo.Value.AddDays(1).Month, dateTo.Value.AddDays(1).Day, 0, 0, 0, DateTimeKind.Unspecified);

            var utcFrom = TimeZoneInfo.ConvertTimeToUtc(localFrom, tz);
            var utcToExclusive = TimeZoneInfo.ConvertTimeToUtc(localToExclusive, tz);

            int? scopedCompanyId = null;
            if (!_currentUser.IsAdmin)
            {
                scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
            }

            var points = await _unitOfWork.GpsTrackings.GetByProfessionalAndRangeAsync(
                professionalId,
                utcFrom,
                utcToExclusive,
                scopedCompanyId);

            // Blindagem: remove pontos inválidos (ex.: 0,0 / sem Location / fora de range)
            points = points
                .Where(IsValidGpsPoint)
                .ToList();

            // agrupa por dia (no fuso)
            var byDay = new Dictionary<DateOnly, List<GpsTracking>>();
            foreach (var p in points)
            {
                var utc = DateTime.SpecifyKind(p.Timestamp, DateTimeKind.Utc);
                var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
                var day = DateOnly.FromDateTime(local.Date);

                if (!byDay.TryGetValue(day, out var list))
                {
                    list = new List<GpsTracking>();
                    byDay[day] = list;
                }
                list.Add(p);
            }

            // garante que dias sem pontos não apareçam (rota vazia não faz sentido)
            var days = byDay.Keys.OrderBy(d => d).ToList();
            var result = new List<GpsRouteDayDTO>(days.Count);

            foreach (var day in days)
            {
                var dayPoints = byDay[day].OrderBy(x => x.Timestamp).ToList();
                if (dayPoints.Count == 0) continue;

                // Robustez/performance: downsampling quando includePoints=true
                var downsampled = includePoints
                    ? DownsampleOrderedPoints(dayPoints, _opts.DownsampleMinSeconds, _opts.DownsampleMinMeters)
                    : dayPoints;

                var first = dayPoints.First();
                var last = dayPoints.Last();

                var summary = new GpsRouteSummaryDTO
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    StartUtc = DateTime.SpecifyKind(first.Timestamp, DateTimeKind.Utc),
                    EndUtc = DateTime.SpecifyKind(last.Timestamp, DateTimeKind.Utc),
                    TotalPoints = dayPoints.Count,
                };

                // distância total
                summary.TotalDistanceKm = CalculateTotalDistanceKm(dayPoints);

                // paradas
                var stops = includeStops ? DetectStops(dayPoints) : new List<GpsRouteStopDTO>();
                summary.TotalStops = stops.Count;

                if (summary.StartUtc.HasValue && summary.EndUtc.HasValue)
                {
                    summary.TotalDurationMinutes = Math.Max(0, (summary.EndUtc.Value - summary.StartUtc.Value).TotalMinutes);
                }

                summary.StoppedMinutes = stops.Sum(s => s.DurationMinutes);
                summary.MovingMinutes = Math.Max(0, summary.TotalDurationMinutes - summary.StoppedMinutes);

                var dto = new GpsRouteDayDTO
                {
                    ProfessionalId = professionalId,
                    CompanyId = first.CompanyId,
                    Summary = summary,
                    Points = includePoints
                        ? downsampled.Select(x => new GpsRoutePointDTO
                        {
                            Latitude = x.Location!.Latitude,
                            Longitude = x.Location!.Longitude,
                            Address = x.Location!.Address,
                            TimestampUtc = DateTime.SpecifyKind(x.Timestamp, DateTimeKind.Utc),
                            Source = x.Source,
                            AppointmentId = x.AppointmentId,
                            CustomerId = x.CustomerId,
                            CheckRecordId = x.CheckRecordId
                        }).ToList()
                        : new List<GpsRoutePointDTO>(),
                    Stops = stops
                };

                result.Add(dto);
            }

            return result;
        }

        private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
        {
            var tzId = string.IsNullOrWhiteSpace(timeZoneId) ? "America/Sao_Paulo" : timeZoneId.Trim();

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            catch
            {
                // Fallback seguro
                return TimeZoneInfo.Utc;
            }
        }

        private static double CalculateTotalDistanceKm(List<GpsTracking> points)
        {
            if (points.Count < 2) return 0;

            double totalMeters = 0;
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1].Location;
                var b = points[i].Location;
                if (a == null || b == null) continue;

                if (!IsValidLatLng(a.Latitude, a.Longitude) || !IsValidLatLng(b.Latitude, b.Longitude))
                    continue;

                var d = HaversineMeters(a.Latitude, a.Longitude, b.Latitude, b.Longitude);

                // filtro leve para “saltos absurdos” (ex.: GPS bug). Ajuste se quiser.
                if (d <= 5000)
                    totalMeters += d;
            }

            return Math.Round(totalMeters / 1000.0, 3);
        }

        private static List<GpsRouteStopDTO> DetectStops(List<GpsTracking> orderedPoints, double stopRadiusMeters = 75, double stopMinMinutes = 5)
        {
            var stops = new List<GpsRouteStopDTO>();
            if (orderedPoints.Count < 2) return stops;

            // Garante segurança caso a lista venha com dados inconsistentes.
            orderedPoints = orderedPoints.Where(IsValidGpsPoint).ToList();
            if (orderedPoints.Count < 2) return stops;

            var anchor = orderedPoints[0];
            var anchorTime = DateTime.SpecifyKind(anchor.Timestamp, DateTimeKind.Utc);

            for (var i = 1; i < orderedPoints.Count; i++)
            {
                var curr = orderedPoints[i];
                var currTime = DateTime.SpecifyKind(curr.Timestamp, DateTimeKind.Utc);

                var dist = HaversineMeters(anchor.Location!.Latitude, anchor.Location!.Longitude, curr.Location!.Latitude, curr.Location!.Longitude);

                // continua dentro da mesma “bolha”
                if (dist <= stopRadiusMeters)
                {
                    continue;
                }

                // saiu da bolha: verifica se ficou parado tempo suficiente
                var prev = orderedPoints[i - 1];
                var prevTime = DateTime.SpecifyKind(prev.Timestamp, DateTimeKind.Utc);
                var duration = (prevTime - anchorTime).TotalMinutes;

                if (duration >= stopMinMinutes)
                {
                    stops.Add(new GpsRouteStopDTO
                    {
                        Latitude = anchor.Location!.Latitude,
                        Longitude = anchor.Location!.Longitude,
                        Address = anchor.Location!.Address,
                        StartUtc = anchorTime,
                        EndUtc = prevTime,
                        DurationMinutes = Math.Round(duration, 2)
                    });
                }

                // reseta anchor
                anchor = curr;
                anchorTime = currTime;
            }

            // fecha última parada (se terminar o dia dentro da bolha)
            var last = orderedPoints.Last();
            var lastTime = DateTime.SpecifyKind(last.Timestamp, DateTimeKind.Utc);
            var tailDuration = (lastTime - anchorTime).TotalMinutes;
            if (tailDuration >= stopMinMinutes)
            {
                stops.Add(new GpsRouteStopDTO
                {
                    Latitude = anchor.Location!.Latitude,
                    Longitude = anchor.Location!.Longitude,
                    Address = anchor.Location!.Address,
                    StartUtc = anchorTime,
                    EndUtc = lastTime,
                    DurationMinutes = Math.Round(tailDuration, 2)
                });
            }

            return stops;
        }

        private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // metros
            double ToRad(double angle) => angle * (Math.PI / 180.0);

            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static List<GpsTracking> DownsampleOrderedPoints(List<GpsTracking> orderedPoints, int minSeconds, double minMeters)
        {
            orderedPoints = orderedPoints.Where(IsValidGpsPoint).OrderBy(x => x.Timestamp).ToList();
            if (orderedPoints.Count <= 2) return orderedPoints;

            var sec = Math.Max(1, minSeconds);
            var meters = Math.Max(0, minMeters);

            var result = new List<GpsTracking>(Math.Min(orderedPoints.Count, 1000));

            var lastKept = orderedPoints[0];
            result.Add(lastKept);

            for (var i = 1; i < orderedPoints.Count - 1; i++)
            {
                var curr = orderedPoints[i];
                var dt = (DateTime.SpecifyKind(curr.Timestamp, DateTimeKind.Utc) - DateTime.SpecifyKind(lastKept.Timestamp, DateTimeKind.Utc)).TotalSeconds;
                if (dt >= sec)
                {
                    result.Add(curr);
                    lastKept = curr;
                    continue;
                }

                if (meters > 0)
                {
                    var dist = HaversineMeters(lastKept.Location!.Latitude, lastKept.Location!.Longitude, curr.Location!.Latitude, curr.Location!.Longitude);
                    if (dist >= meters)
                    {
                        result.Add(curr);
                        lastKept = curr;
                    }
                }
            }

            // sempre inclui o último
            result.Add(orderedPoints[^1]);
            return result;
        }

        private static bool IsValidGpsPoint(GpsTracking p)
        {
            if (p.Location == null) return false;
            return IsValidLatLng(p.Location.Latitude, p.Location.Longitude);
        }

        private static bool IsValidLatLng(double lat, double lng)
        {
            if (double.IsNaN(lat) || double.IsNaN(lng)) return false;
            if (double.IsInfinity(lat) || double.IsInfinity(lng)) return false;
            if (lat == 0 && lng == 0) return false;
            if (lat < -90 || lat > 90) return false;
            if (lng < -180 || lng > 180) return false;
            return true;
        }
    }
}
