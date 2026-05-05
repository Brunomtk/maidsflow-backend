using Core.DTO.Messaging;

namespace Services.Messaging
{
    /// <summary>
    /// Resolves which Twilio sender phone (or whether to block) a given company should use
    /// for outbound SMS, applying the MaidsFlow trial + A2P 10DLC compliance rules.
    /// </summary>
    public interface ISmsSenderResolver
    {
        /// <summary>
        /// Determine the proper sender for the given company at this moment.
        /// </summary>
        Task<SmsSenderDecisionDTO> ResolveAsync(int companyId, CancellationToken ct = default);
    }
}
