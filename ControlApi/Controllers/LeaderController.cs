using Core.DTO.Leader;
using Core.Enums;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LeaderController : ControllerBase
    {
        private readonly ILeaderService _leaderService;

        public LeaderController(ILeaderService leaderService)
        {
            _leaderService = leaderService;
        }

        /// <summary>
        /// Lista todos os líderes.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var leaders = await _leaderService.GetAllAsync();

            var result = leaders.Select(l => new LeaderDTO
            {
                Id = l.Id,
                Name = l.Name,
                Email = l.Email,
                Phone = l.Phone,
                Status = l.Status.ToString(),
                CreatedDate = l.CreatedDate
            });

            return Ok(result);
        }

        /// <summary>
        /// Retorna um líder pelo ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var leader = await _leaderService.GetByIdAsync(id);
            if (leader == null) return NotFound();

            var dto = new LeaderDTO
            {
                Id = leader.Id,
                Name = leader.Name,
                Email = leader.Email,
                Phone = leader.Phone,
                Status = leader.Status.ToString(),
                CreatedDate = leader.CreatedDate
            };

            return Ok(dto);
        }

        /// <summary>
        /// Cria um novo líder.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateLeaderRequest request)
        {
            var leader = new Leader
            {
                UserId = request.UserId,
                Region = request.Region,
                Name = request.Name,
                Email = request.Email,
                Phone = request.Phone,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow,
                Status = StatusEnum.Active
            };

            var created = await _leaderService.CreateAsync(leader);

            var dto = new LeaderDTO
            {
                Id = created.Id,
                Name = created.Name,
                Email = created.Email,
                Phone = created.Phone,
                Status = created.Status.ToString(),
                CreatedDate = created.CreatedDate
            };

            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        /// <summary>
        /// Atualiza um líder existente.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateLeaderRequest request)
        {
            var updatedLeader = new Leader
            {
                Name = request.Name ?? string.Empty,
                Email = request.Email,
                Phone = request.Phone,
                Status = Enum.TryParse<StatusEnum>(request.Status, true, out var status)
                    ? status
                    : StatusEnum.Inactive,
                UpdatedDate = DateTime.UtcNow
            };

            var result = await _leaderService.UpdateAsync(id, updatedLeader);
            if (result == null) return NotFound();

            var dto = new LeaderDTO
            {
                Id = result.Id,
                Name = result.Name,
                Email = result.Email,
                Phone = result.Phone,
                Status = result.Status.ToString(),
                CreatedDate = result.CreatedDate
            };

            return Ok(dto);
        }

        /// <summary>
        /// Deleta um líder.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _leaderService.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
