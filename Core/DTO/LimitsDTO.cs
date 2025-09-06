namespace Core.DTO
{
    /// <summary>
    /// Define os limites numéricos associados a um plano.
    /// </summary>
    public class LimitsDTO
    {
        /// <summary>
        /// Quantidade máxima de profissionais permitidos.
        /// </summary>
        public int? Professionals { get; set; }

        /// <summary>
        /// Quantidade máxima de equipes permitidas.
        /// </summary>
        public int? Teams { get; set; }

        /// <summary>
        /// Quantidade máxima de clientes permitidos.
        /// </summary>
        public int? Customers { get; set; }

        /// <summary>
        /// Quantidade máxima de agendamentos permitidos.
        /// </summary>
        public int? Appointments { get; set; }
    }
}
