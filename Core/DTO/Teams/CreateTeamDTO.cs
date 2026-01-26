using System.Collections.Generic;

namespace Core.DTO.Teams
{
    public class CreateTeamDTO
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Optional UI color for the team. Recommended format: hex (#RRGGBB or #RRGGBBAA).
        /// </summary>
        public string? Color { get; set; }
        public string? Region { get; set; }
        public string? Description { get; set; }
        public int CompanyId { get; set; }

        /// <summary>
        /// Status da equipe (1 = Ativo, 2 = Inativo).
        /// </summary>
        public int Status { get; set; } = 1;

        /// <summary>
        /// Lista de membros da equipe.
        /// </summary>
        public List<TeamMemberDTO>? Members { get; set; }
    }
}
