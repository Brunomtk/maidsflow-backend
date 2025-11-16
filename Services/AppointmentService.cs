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
            // Nesta versão, NÃO fazemos conversão de fuso horário.
            // O horário enviado no DTO (start/end) é salvo exatamente como veio,
            // para que o banco sempre reflita o horário local informado pelo usuário.

            var appointment = new Appointment
            {
                Title = dto.Title,
                Address = dto.Address,
                Start = dto.Start,
                End = dto.End,
                Notes = dto.Notes,
                Status = dto.Status ?? AppointmentStatus.Scheduled,
                Type = dto.Type ?? AppointmentType.Regular,
                CompanyId = dto.CompanyId,
                CustomerId = dto.CustomerId,
                TeamId = dto.TeamId,
                ProfessionalId = dto.ProfessionalId,
                // TimeZoneId vira apenas informação complementar, não afeta o horário salvo.
                TimeZoneId = dto.TimeZoneId,
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

            // Atualiza campos básicos
            appointment.Title = dto.Title ?? appointment.Title;
            appointment.Address = dto.Address ?? appointment.Address;

            // Aqui também NÃO fazemos conversão de fuso horário.
            // Se vier start/end no DTO, salvamos exatamente o que veio.
            if (dto.Start.HasValue)
            {
                appointment.Start = dto.Start.Value;
            }

            if (dto.End.HasValue)
            {
                appointment.End = dto.End.Value;
            }

            appointment.Notes = dto.Notes ?? appointment.Notes;
            appointment.Status = dto.Status ?? appointment.Status;
            appointment.Type = dto.Type ?? appointment.Type;
            appointment.CompanyId = dto.CompanyId ?? appointment.CompanyId;
            appointment.CustomerId = dto.CustomerId ?? appointment.CustomerId;
            appointment.TeamId = dto.TeamId ?? appointment.TeamId;
            appointment.ProfessionalId = dto.ProfessionalId ?? appointment.ProfessionalId;

            // Atualiza campos de recorrência/timezone se vierem
            if (!string.IsNullOrWhiteSpace(dto.TimeZoneId))
                appointment.TimeZoneId = dto.TimeZoneId;

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
