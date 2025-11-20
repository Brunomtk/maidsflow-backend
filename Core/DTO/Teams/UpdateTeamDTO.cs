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
        public int LeaderId { get; set; }
        public string Region { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CompanyId { get; set; }

        public List<TeamMemberDTO>? Members { get; set; }
    }
}
