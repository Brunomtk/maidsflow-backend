using Core.DTO.Appointment;
using Core.Enums.Appointment;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;

namespace Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly Infrastructure.Repositories.IUnitOfWork _unitOfWork;

        public AppointmentService(Infrastructure.Repositories.IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PagedResult<Appointment>> GetPagedAppointments(AppointmentFiltersDTO filters)
        {
            return await _unitOfWork.Appointments.GetPagedAppointmentsAsync(filters);
        }

        public async Task<List<Appointment>> GetByCompany(int companyId)
        {
            return await _unitOfWork.Appointments.GetAppointmentsByCompanyAsync(companyId);
        }

        public async Task<List<Appointment>> GetByTeam(int teamId)
        {
            return await _unitOfWork.Appointments.GetAppointmentsByTeamAsync(teamId);
        }

        public async Task<List<Appointment>> GetByProfessional(int professionalId)
        {
            return await _unitOfWork.Appointments.GetAppointmentsByProfessionalAsync(professionalId);
        }

        public async Task<List<Appointment>> GetByCustomer(int customerId)
        {
            return await _unitOfWork.Appointments.GetAppointmentsByCustomerAsync(customerId);
        }

        public async Task<List<Appointment>> GetByDateRange(DateTime start, DateTime end, int? companyId = null)
        {
            return await _unitOfWork.Appointments.GetAppointmentsByDateRangeAsync(start, end, companyId);
        }

        public async Task<Appointment?> GetById(int id)
        {
            return await _unitOfWork.Appointments.GetById(id);
        }

        public async Task<bool> Create(CreateAppointmentDTO dto)
        {
            // Define o timezone: usa o enviado no DTO ou um padrão da aplicação
            var timeZoneId = string.IsNullOrWhiteSpace(dto.TimeZoneId)
                ? "America/Sao_Paulo"
                : dto.TimeZoneId;

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

            // A UI manda a data/hora como horário local. Garantimos que o Kind seja Unspecified
            var startLocal = DateTime.SpecifyKind(dto.Start, DateTimeKind.Unspecified);
            var endLocal = DateTime.SpecifyKind(dto.End, DateTimeKind.Unspecified);

            // Converte de horário local para UTC para salvar em coluna timestamptz
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone);

            var appointment = new Appointment
            {
                Title = dto.Title,
                Address = dto.Address,
                Start = startUtc,
                End = endUtc,
                Notes = dto.Notes,
                Status = dto.Status ?? AppointmentStatus.Scheduled,
                Type = dto.Type ?? AppointmentType.Regular,
                CompanyId = dto.CompanyId,
                CustomerId = dto.CustomerId,
                TeamId = dto.TeamId,
                ProfessionalId = dto.ProfessionalId,
                TimeZoneId = timeZoneId,
                IsRecurring = dto.IsRecurring,
                RecurrenceRule = dto.RecurrenceRule,
                RecurrenceEnd = dto.RecurrenceEnd,
                OccurrenceCount = dto.OccurrenceCount
            };

            await _unitOfWork.Appointments.Add(appointment);
            return await _unitOfWork.SaveAsync() > 0;
        }

        public async Task<bool> Update(int id, UpdateAppointmentDTO dto)
        {
            var appointment = await _unitOfWork.Appointments.GetById(id);
            if (appointment == null) return false;

            // Descobre o timezone a usar: DTO > existente > padrão
            var timeZoneId = !string.IsNullOrWhiteSpace(dto.TimeZoneId)
                ? dto.TimeZoneId
                : !string.IsNullOrWhiteSpace(appointment.TimeZoneId)
                    ? appointment.TimeZoneId
                    : "America/Sao_Paulo";

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

            appointment.Title = dto.Title ?? appointment.Title;
            appointment.Address = dto.Address ?? appointment.Address;

            if (dto.Start.HasValue)
            {
                var startLocal = DateTime.SpecifyKind(dto.Start.Value, DateTimeKind.Unspecified);
                appointment.Start = TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone);
            }

            if (dto.End.HasValue)
            {
                var endLocal = DateTime.SpecifyKind(dto.End.Value, DateTimeKind.Unspecified);
                appointment.End = TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone);
            }

            appointment.Notes = dto.Notes ?? appointment.Notes;
            appointment.Status = dto.Status ?? appointment.Status;
            appointment.Type = dto.Type ?? appointment.Type;
            appointment.CompanyId = dto.CompanyId ?? appointment.CompanyId;
            appointment.CustomerId = dto.CustomerId ?? appointment.CustomerId;
            appointment.TeamId = dto.TeamId ?? appointment.TeamId;
            appointment.ProfessionalId = dto.ProfessionalId ?? appointment.ProfessionalId;

            // Atualiza campos de recorrência/timezone se vierem
            appointment.TimeZoneId = timeZoneId;
            if (dto.IsRecurring.HasValue) appointment.IsRecurring = dto.IsRecurring.Value;
            if (dto.RecurrenceRule != null) appointment.RecurrenceRule = dto.RecurrenceRule;
            if (dto.RecurrenceEnd.HasValue) appointment.RecurrenceEnd = dto.RecurrenceEnd;
            if (dto.OccurrenceCount.HasValue) appointment.OccurrenceCount = dto.OccurrenceCount;

            _unitOfWork.Appointments.Update(appointment);
            return await _unitOfWork.SaveAsync() > 0;
        }

        public async Task<bool> Delete(int id)
        {
            var appointment = await _unitOfWork.Appointments.GetById(id);
            if (appointment == null) return false;

            _unitOfWork.Appointments.Delete(appointment);
            return await _unitOfWork.SaveAsync() > 0;
        }
    }

    public interface IAppointmentService
    {
        Task<PagedResult<Appointment>> GetPagedAppointments(AppointmentFiltersDTO filters);
        Task<List<Appointment>> GetByCompany(int companyId);
        Task<List<Appointment>> GetByTeam(int teamId);
        Task<List<Appointment>> GetByProfessional(int professionalId);
        Task<List<Appointment>> GetByCustomer(int customerId);
        Task<List<Appointment>> GetByDateRange(DateTime start, DateTime end, int? companyId = null);
        Task<Appointment?> GetById(int id);
        Task<bool> Create(CreateAppointmentDTO dto);
        Task<bool> Update(int id, UpdateAppointmentDTO dto);
        Task<bool> Delete(int id);
    }
}
