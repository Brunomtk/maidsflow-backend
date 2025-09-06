using System.Threading.Tasks;
using Core.DTO.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace ControlApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        [AllowAnonymous]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateReviewDTO request)
        {
            var created = await _reviewService.CreateAsync(request);
            return created != null
                ? Ok(created)
                : BadRequest("Failed to create review");
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var review = await _reviewService.GetByIdAsync(id);
            return review != null
                ? Ok(review)
                : NotFound("Review not found");
        }

        /// <summary>
        /// Lista paginada de avaliações com filtros opcionais.
        /// </summary>
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] ReviewFiltersDTO filters)
        {
            var result = await _reviewService.GetPagedAsync(filters);
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
        public async Task<IActionResult> Update(int id, [FromBody] UpdateReviewDTO request)
        {
            var updated = await _reviewService.UpdateAsync(id, request);
            return updated != null
                ? Ok(updated)
                : BadRequest("Failed to update review");
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _reviewService.DeleteAsync(id);
            return deleted
                ? Ok(true)
                : NotFound("Review not found");
        }

        /// <summary>
        /// Registra uma resposta à avaliação.
        /// </summary>
        [HttpPost("{id:int}/response")]
        public async Task<IActionResult> Respond(int id, [FromBody] string response)
        {
            var updated = await _reviewService.RespondAsync(id, response);
            return updated != null
                ? Ok(updated)
                : NotFound("Review not found");
        }
    }
}
