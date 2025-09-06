// ControlApi/Controllers/GpsTrackingController.cs
using System.Threading.Tasks;
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
    }
}
