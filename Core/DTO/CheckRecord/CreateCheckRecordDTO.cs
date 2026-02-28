using System;

namespace Core.DTO.CheckRecord
{
    public class CreateCheckRecordDTO
    {
        public int ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }

        public int CompanyId { get; set; }

        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public int AppointmentId { get; set; }

        public string Address { get; set; } = string.Empty;

        // Coordenadas do check-in (opcional). Se não vier, o sistema não cria ponto GPS automático.
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public int? TeamId { get; set; }
        public string? TeamName { get; set; }

        public string ServiceType { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }
}
