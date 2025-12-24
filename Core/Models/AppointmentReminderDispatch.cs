using System;
using Core.Enums.Notifications;

namespace Core.Models
{
    /// <summary>
    /// Log de envios de lembretes automáticos (idempotência).
    /// Evita reenviar o mesmo lembrete para o mesmo usuário e ocorrência.
    /// </summary>
    public class AppointmentReminderDispatch : BaseModel
    {
        public int AppointmentId { get; set; }
        /// <summary>
        /// SeriesId da recorrência (quando o appointment é parte de uma série).
        /// Para appointments não recorrentes fica null.
        /// </summary>
        public Guid? SeriesId { get; set; }
        public DateTime OccurrenceStartUtc { get; set; }

        public int RecipientUserId { get; set; }
        public ReminderType ReminderType { get; set; }
    }
}
