namespace Core.Enums.GpsTracking
{
    public enum GpsTrackingSource
    {
        /// <summary>Coleta contínua do GPS do dispositivo.</summary>
        Gps = 0,

        /// <summary>Ponto criado automaticamente no momento do check-in.</summary>
        CheckIn = 1,

        /// <summary>Ponto criado automaticamente no momento do check-out (opcional).</summary>
        CheckOut = 2
    }
}
