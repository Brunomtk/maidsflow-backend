namespace Core.Models
{
    /// <summary>
    /// Represents a member of a team, linking a professional to a team with an optional description.
    /// </summary>
    public class TeamMember : BaseModel
    {
        public int TeamId { get; set; }
        public Team Team { get; set; } = null!;

        public int ProfessionalId { get; set; }
        public Professional Professional { get; set; } = null!;

        public int? UserId { get; set; }
        public User? User { get; set; }


        /// <summary>
        /// Optional description for this member inside the team (e.g., role, notes).
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Indicates if this member is the leader of the team. Only one leader is allowed per team.
        /// </summary>
        public bool IsLeader { get; set; }
    }
}