// ControlApi/Controllers/GpsTrackingController.cs
using System.Threading.Tasks;
using System;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.DTO.GpsTracking;
using Services;   // ← certifica-se de que este namespace é o correto

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GpsTrackingController : ControllerBase
    {
        private readonly IGpsTrackingService _gpsTrackingService;

        public GpsTrackingController(IGpsTrackingService gpsTrackingService)
        {
            _gpsTrackingService = gpsTrackingService;
        }

        [AllowAnonymous]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateGpsTrackingDTO request)
        {
            var created = await _gpsTrackingService.CreateAsync(request);
            return created != null
                ? Ok(created)
                : BadRequest("Failed to create GPS tracking record");
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var record = await _gpsTrackingService.GetByIdAsync(id);
            return record != null
                ? Ok(record)
                : NotFound("GPS tracking record not found");
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] GpsTrackingFiltersDTO filters)
        {
            var result = await _gpsTrackingService.GetPagedAsync(filters);
            return Ok(new
            {
                data = result.Results,
                meta = new
                {
                    currentPage = result.CurrentPage,
                    totalPages = result.PageCount,
                    totalItems = result.TotalItems,
                    itemsPerPage = result.PageSize
                }
            });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateGpsTrackingDTO request)
        {
            var updated = await _gpsTrackingService.UpdateAsync(id, request);
            return updated != null
                ? Ok(true)
                : BadRequest("Failed to update GPS tracking record");
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _gpsTrackingService.DeleteAsync(id);
            return deleted
                ? Ok(true)
                : NotFound("GPS tracking record not found");
        }

        /// <summary>
        /// Mostra as rotas de um profissional (agrupadas por dia). Por padrão, retorna o dia atual.
        /// Datas são interpretadas no fuso informado (default: America/Sao_Paulo) e o resultado devolve timestamps em UTC.
        /// </summary>
        [HttpGet("professional/{professionalId:int}/routes")]
        public async Task<IActionResult> GetRoutesByProfessional(
            int professionalId,
            [FromQuery] string? dateFrom,
            [FromQuery] string? dateTo,
            [FromQuery] string? timeZoneId,
            [FromQuery] bool includePoints = true,
            [FromQuery] bool includeStops = true)
        {
            DateOnly? from = ParseDateOnly(dateFrom);
            DateOnly? to = ParseDateOnly(dateTo);

            var routes = await _gpsTrackingService.GetProfessionalRoutesAsync(
                professionalId,
                from,
                to,
                timeZoneId,
                includePoints,
                includeStops);

            return Ok(routes);
        }

        private static DateOnly? ParseDateOnly(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            // Aceita YYYY-MM-DD (recomendado) e também formatos comuns.
            if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;

            if (DateOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return d;

            return null;
        }
    }
}
