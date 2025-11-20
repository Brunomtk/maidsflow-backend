using Core.DTO.Teams;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using System.Linq;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TeamController : ControllerBase
    {
        private readonly ITeamService _teamService;

        public TeamController(ITeamService teamService)
        {
            _teamService = teamService;
        }

        /// <summary>
        /// Lista paginada simples de equipes.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string status = "all",
            [FromQuery] string? search = null)
        {
            var result = await _teamService.GetPagedTeams(page, pageSize, status, search);
            return Ok(result);
        }

        /// <summary>
        /// Lista paginada usando TeamFiltersDTO.
        /// </summary>
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] TeamFiltersDTO filters)
        {
            var result = await _teamService.GetPagedTeams(filters);
            return Ok(result);
        }

        /// <summary>
        /// Detalhe de uma equipe (já com Members).
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var team = await _teamService.GetByIdAsync(id);
            if (team == null) return NotFound();
            return Ok(team);
        }

        /// <summary>
        /// Cria uma nova equipe.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTeamDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (dto.Members != null && dto.Members.Count(m => m.IsLeader) > 1)
            {
                ModelState.AddModelError("Members", "Somente um membro pode ser líder da equipe.");
                return BadRequest(ModelState);
            }

            var team = new Team
            {
                Name = dto.Name,
                Region = dto.Region ?? string.Empty,
                Description = dto.Description ?? string.Empty,
                CompanyId = dto.CompanyId,
                LeaderId = dto.LeaderId
            };

            if (dto.Members != null)
            {
                foreach (var memberDto in dto.Members)
                {
                    team.Members.Add(new TeamMember
                    {
                        ProfessionalId = memberDto.ProfessionalId,
                        UserId = memberDto.UserId,
                        Description = memberDto.Description,
                        IsLeader = memberDto.IsLeader
                    });
                }
            }

            var created = await _teamService.CreateAsync(team);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        /// <summary>
        /// Atualiza uma equipe existente.
        /// Payload igual ao create, só que UpdateTeamDTO.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTeamDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (dto.Members != null && dto.Members.Count(m => m.IsLeader) > 1)
            {
                ModelState.AddModelError("Members", "Somente um membro pode ser líder da equipe.");
                return BadRequest(ModelState);
            }

            // Carrega a equipe existente
            var team = await _teamService.GetByIdAsync(id);
            if (team == null) return NotFound();

            // Atualiza campos principais
            team.Name = dto.Name;
            team.Region = dto.Region;
            team.Description = dto.Description;
            team.CompanyId = dto.CompanyId;
            team.LeaderId = dto.LeaderId;

            // Atualiza membros
            team.Members.Clear();
            if (dto.Members != null)
            {
                foreach (var memberDto in dto.Members)
                {
                    team.Members.Add(new TeamMember
                    {
                        ProfessionalId = memberDto.ProfessionalId,
                        UserId = memberDto.UserId,
                        Description = memberDto.Description,
                        IsLeader = memberDto.IsLeader
                    });
                }
            }

            var result = await _teamService.UpdateAsync(id, team);
            if (result == null) return NotFound();
            return Ok(result);
        }

        /// <summary>
        /// Remove uma equipe.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _teamService.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
