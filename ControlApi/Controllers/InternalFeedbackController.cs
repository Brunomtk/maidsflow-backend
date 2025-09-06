using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using Core.DTO.InternalFeedback;
using Core.Models;
using System.Threading.Tasks;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InternalFeedbackController : ControllerBase
    {
        private readonly IInternalFeedbackService _service;

        public InternalFeedbackController(IInternalFeedbackService service)
        {
            _service = service;
        }

        /// <summary>
        /// Returns a paged list of internal feedbacks.
        /// </summary>
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] InternalFeedbackFiltersDTO filters)
        {
            var result = await _service.GetPagedAsync(filters);
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

        /// <summary>
        /// Returns a single feedback by ID.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var feedback = await _service.GetByIdAsync(id);
            if (feedback == null) return NotFound();
            return Ok(feedback);
        }

        /// <summary>
        /// Creates a new internal feedback.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateInternalFeedbackDTO dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        /// <summary>
        /// Updates an existing internal feedback.
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateInternalFeedbackDTO dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        /// <summary>
        /// Deletes an internal feedback.
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }

        /// <summary>
        /// Adds a comment to an internal feedback.
        /// </summary>
        [HttpPost("{id:int}/comments")]
        public async Task<IActionResult> AddComment(int id, [FromBody] CreateInternalFeedbackCommentDTO dto)
        {
            var comment = await _service.AddCommentAsync(id, dto);
            return Ok(comment);
        }
    }
}
