namespace Core.Enums.Messaging;

public enum AppointmentMessageKind
{
    // Defaults required by the UI
    ReminderEmail48h = 1,
    ConfirmationSms24h = 2,

    // Operational / realtime
    OnMyWaySms = 10,
    OnMyWayEmail = 11,
}
