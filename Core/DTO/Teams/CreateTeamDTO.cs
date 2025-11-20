using System.Collections.Generic;

namespace Core.DTO.Teams
{
    public class CreateTeamDTO
    {
        public string Name { get; set; } = string.Empty;
        public int? LeaderId { get; set; }
        public string? Region { get; set; }
        public string? Description { get; set; }
        public int CompanyId { get; set; }

        /// <summary>
        /// Optional list of members for this team.
        /// Each member links a professional (and optionally a user) to the team,
        /// with a description and a leader flag.
        /// </summary>
        public List<TeamMemberDTO>? Members { get; set; }
    }
}
