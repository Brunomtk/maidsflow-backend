namespace Core.Enums.User
{
    /// <summary>
    /// Permissions that can be granted to a user.
    /// Names are in English for consistency with the frontend.
    /// </summary>
    public enum UserPermissionCode
    {
        /// <summary>
        /// Can view customer information (Mostrar as informacoes dos clientes)
        /// </summary>
        ViewCustomerInfo = 1,

        /// <summary>
        /// Can cancel appointments (Pode cancelar agendamentos)
        /// </summary>
        CancelAppointment = 2
    }
}
