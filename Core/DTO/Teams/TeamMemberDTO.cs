using Core.Enums.Team;

namespace Core.DTO.Teams
{
    public class TeamMemberDTO
    {
        /// <summary>
        /// Professional ID linked to this team.
        /// </summary>
        public int ProfessionalId { get; set; }

        /// <summary>
        /// Optional user ID linked to this team member (application user).
        /// </summary>
        public int? UserId { get; set; }


        /// <summary>
        /// Optional description for this professional inside the team (role, notes, etc.).
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Role of this member in the team.
        /// 0 = Member (General Member)
        /// 1 = Leader (Team Leader)
        /// </summary>
        public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;

        /// <summary>
        /// Indicates if this member is the leader of the team. Only one leader is allowed per team.
        /// </summary>
        public bool IsLeader { get; set; }
    }
}