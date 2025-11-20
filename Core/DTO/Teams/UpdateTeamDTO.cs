using System.Collections.Generic;

namespace Core.DTO.Teams
{
    /// <summary>
    /// DTO para edição de equipe.
    /// Mantém o mesmo formato do CreateTeamDTO,
    /// para você poder reaproveitar o mesmo form no front.
    /// </summary>
    public class UpdateTeamDTO
    {
        public string Name { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CompanyId { get; set; }

        /// <summary>
        /// Status da equipe (1 = Ativo, 2 = Inativo).
        /// </summary>
        public int Status { get; set; } = 1;

        public List<TeamMemberDTO>? Members { get; set; }
    }
}
