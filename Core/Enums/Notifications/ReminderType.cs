namespace Core.Enums.Notifications
{
    /// <summary>
    /// Tipos de lembrete automáticos (para idempotência/dispatch log).
    /// </summary>
    public enum ReminderType
    {
        Minutes30Before = 1,
        CheckoutMissingAfterEnd10Min = 2
    }
}
