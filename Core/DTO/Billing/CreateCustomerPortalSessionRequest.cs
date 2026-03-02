namespace Core.DTO.Billing
{
    public class CreateCustomerPortalSessionRequest
    {
        /// <summary>
        /// (Admin only) CompanyId para abrir o portal de uma empresa específica.
        /// Company (não admin) ignora este campo.
        /// </summary>
        public int? CompanyId { get; set; }

        /// <summary>
        /// URL para retornar após o cliente terminar no portal.
        /// Ex: https://app.seudominio.com/company/plan
        /// </summary>
        public string? ReturnUrl { get; set; }

        /// <summary>
        /// Se true (default), abre direto no fluxo de atualização de cartão (payment_method_update).
        /// Se false, abre no portal "home" padrão.
        /// </summary>
        public bool PaymentMethodUpdateOnly { get; set; } = true;
    }
}
