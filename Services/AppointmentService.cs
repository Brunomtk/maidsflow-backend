using Core.DTO.Appointment;
using Core.Enums.Appointment;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;

namespace Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AppointmentService(IUnitOfWork unitOfWork)
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
                ProfessionalId = dto.ProfessionalId
            };

            await _unitOfWork.Appointments.Add(appointment);
            return await _unitOfWork.SaveAsync() > 0;
        }

        public async Task<bool> Update(int id, UpdateAppointmentDTO dto)
        {
            var appointment = await _unitOfWork.Appointments.GetById(id);
            if (appointment == null) return false;

            appointment.Title = dto.Title ?? appointment.Title;
            appointment.Address = dto.Address ?? appointment.Address;
            appointment.Start = dto.Start ?? appointment.Start;
            appointment.End = dto.End ?? appointment.End;
            appointment.Notes = dto.Notes ?? appointment.Notes;
            appointment.Status = dto.Status ?? appointment.Status;
            appointment.Type = dto.Type ?? appointment.Type;
            appointment.CompanyId = dto.CompanyId ?? appointment.CompanyId;
            appointment.CustomerId = dto.CustomerId ?? appointment.CustomerId;
            appointment.TeamId = dto.TeamId ?? appointment.TeamId;
            appointment.ProfessionalId = dto.ProfessionalId ?? appointment.ProfessionalId;

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
