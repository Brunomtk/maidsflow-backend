namespace Core.DTO.Billing
{
    public class CancelStripeSubscriptionRequest
    {
        /// <summary>
        /// (Admin only) CompanyId para cancelar uma empresa específica. Company (não admin) ignora este campo.
        /// </summary>
        public int? CompanyId { get; set; }

        /// <summary>
        /// Se true: cancela imediatamente (corta o acesso agora).
        /// Se false: agenda cancelamento no fim do período (cancel_at_period_end=true).
        /// </summary>
        public bool Immediate { get; set; } = false;
    }
}
